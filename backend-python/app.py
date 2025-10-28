import os
import time
import json
from datetime import datetime, date

from flask import Flask, request, jsonify
from flask_cors import CORS

from face_engine import FaceEngine
import numpy as np
from scipy.spatial.distance import cosine

import cv2
import mediapipe as mp

from pymongo import MongoClient

# ----------------------------------------------------------------------------
# Flask + Mongo
# ----------------------------------------------------------------------------
app = Flask(__name__)
CORS(app)

engine = FaceEngine()

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
os.makedirs(DATA_DIR, exist_ok=True)

THRESHOLD = 0.55

MONGO_URI = os.getenv("MONGO_URI", "mongodb://localhost:27017")
client = MongoClient(MONGO_URI)
db = client["face_attendance"]
attendance_col = db["attendance"]

# ----------- Đảm bảo index (ma_nv, ngay, session) là unique -----------------
target_keys = [("ma_nv", 1), ("ngay", 1), ("session", 1)]
index_name = "ma_nv_ngay_session"

indexes = attendance_col.index_information()

# dọn index cũ nếu còn
if "ma_nv_1_ngay_1" in indexes:
    attendance_col.drop_index("ma_nv_1_ngay_1")

existing = None
for name, info in indexes.items():
    keys = info.get("key")
    if keys == target_keys:
        existing = (name, info)
        break

if existing:
    name, info = existing
    if not info.get("unique", False):
        attendance_col.drop_index(name)
        attendance_col.create_index(target_keys, unique=True, name=index_name)
else:
    attendance_col.create_index(target_keys, unique=True, name=index_name)

# ----------------------------------------------------------------------------
# Mediapipe pose helper ...
# ----------------------------------------------------------------------------
mp_face_mesh = mp.solutions.face_mesh.FaceMesh(
    static_image_mode=False,
    max_num_faces=1,
    refine_landmarks=True,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)
_previous_gray = None

FACE_3D_POINTS = np.array([
    (0.0, 0.0, 0.0),
    (0.0, -63.6, -12.5),
    (-43.3, 32.7, -26.0),
    (43.3, 32.7, -26.0),
    (-28.9, -28.9, -24.1),
    (28.9, -28.9, -24.1),
], dtype=np.float64)
LANDMARK_IDX = [1, 199, 33, 263, 61, 291]


def normalize(vec):
    vec = np.asarray(vec, dtype=np.float32)
    norm = np.linalg.norm(vec)
    return vec if norm == 0 else vec / norm


def clamp_angle(angle: float) -> float:
    if angle > 90:
        return 180 - angle
    if angle < -90:
        return -180 - angle
    return angle


def estimate_pose_and_motion(img_bytes: bytes):
    global _previous_gray
    np_img = np.frombuffer(img_bytes, np.uint8)
    frame = cv2.imdecode(np_img, cv2.IMREAD_COLOR)
    if frame is None:
        return None

    frame = cv2.flip(frame, 1)

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    motion_score = float(cv2.absdiff(_previous_gray, gray).mean()) if _previous_gray is not None else 0.0
    _previous_gray = gray

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    results = mp_face_mesh.process(rgb)
    if not results.multi_face_landmarks:
        return None

    h, w = frame.shape[:2]
    face_landmarks = results.multi_face_landmarks[0]
    points_2d = np.array([[face_landmarks.landmark[i].x * w,
                           face_landmarks.landmark[i].y * h] for i in LANDMARK_IDX],
                         dtype=np.float64)

    focal_length = w
    camera_matrix = np.array([
        [focal_length, 0, w / 2],
        [0, focal_length, h / 2],
        [0, 0, 1]
    ], dtype=np.float64)
    dist_coeffs = np.zeros((4, 1))

    success, rotation_vector, translation_vector = cv2.solvePnP(
        FACE_3D_POINTS, points_2d, camera_matrix, dist_coeffs, flags=cv2.SOLVEPNP_ITERATIVE
    )
    if not success:
        return None

    rmat, _ = cv2.Rodrigues(rotation_vector)
    sy = np.sqrt(rmat[0, 0] ** 2 + rmat[1, 0] ** 2)
    singular = sy < 1e-6

    if not singular:
        x = np.arctan2(rmat[2, 1], rmat[2, 2])
        y = np.arctan2(-rmat[2, 0], sy)
        z = np.arctan2(rmat[1, 0], rmat[0, 0])
    else:
        x = np.arctan2(-rmat[1, 2], rmat[1, 1])
        y = np.arctan2(-rmat[2, 0], sy)
        z = 0

    pitch, yaw, roll = np.degrees([x, y, z])
    pitch = -pitch

    yaw = ((yaw + 180) % 360) - 180
    pitch = ((pitch + 180) % 360) - 180
    roll = ((roll + 180) % 360) - 180

    yaw = clamp_angle(yaw)
    pitch = clamp_angle(pitch)
    roll = clamp_angle(roll)

    direction = "Center"
    if yaw > 15:
        direction = "Right"
    elif yaw < -15:
        direction = "Left"
    elif pitch > 15:
        direction = "Down"
    elif pitch < -15:
        direction = "Up"

    print(f"[POSE] yaw={yaw:.2f}, pitch={pitch:.2f}, roll={roll:.2f}, motion={motion_score:.2f}")
    return yaw, pitch, roll, motion_score, direction

# ----------------------------------------------------------------------------
# Ghi nhận chấm công: vào - ra - vào - ra ...
# ----------------------------------------------------------------------------
def record_attendance(ma_nv: str, ten_nv: str):
    today = date.today().isoformat()
    now = datetime.now()
    time_str = now.strftime("%H:%M:%S")

    # Nếu còn ca nào chưa có giờ ra -> cập nhật giờ ra
    open_session = attendance_col.find_one(
        {"ma_nv": ma_nv, "ngay": today, "check_out": None},
        sort=[("session", 1)]
    )

    if open_session:
        attendance_col.update_one(
            {"_id": open_session["_id"]},
            {"$set": {"check_out": time_str, "updated_at": now}}
        )
        print(f"[ATTENDANCE] UPDATE -> {ma_nv} {today} session {open_session['session']} check_out={time_str}")
        return "out", open_session["session"], open_session["check_in"], time_str

    # Ngược lại -> tạo ca mới (giờ vào mới)
    latest = attendance_col.find_one(
        {"ma_nv": ma_nv, "ngay": today},
        sort=[("session", -1)]
    )
    next_session = (latest["session"] + 1) if latest else 1

    doc = {
        "ma_nv": ma_nv,
        "ten_nv": ten_nv,
        "ngay": today,
        "session": next_session,
        "check_in": time_str,
        "check_out": None,
        "created_at": now,
        "updated_at": now
    }
    attendance_col.insert_one(doc)
    print(f"[ATTENDANCE] INSERT -> {ma_nv} {today} session {next_session} check_in={time_str}")
    return "in", next_session, time_str, None

# ----------------------------------------------------------------------------
# Các route Flask (pose, register, checkin, report)
# ----------------------------------------------------------------------------
@app.route("/")
def home():
    return jsonify({"message": "Server Flask đang chạy!"})

@app.route("/pose", methods=["POST"])
def pose():
    if "image" not in request.files:
        return jsonify({"error": "Thiếu ảnh để kiểm tra pose"}), 400

    img_bytes = request.files["image"].read()
    pose_result = estimate_pose_and_motion(img_bytes)

    if pose_result is None:
        print("[POSE] Không tìm thấy mặt.")
        return jsonify({"error": "Không phát hiện khuôn mặt"}), 400

    yaw, pitch, roll, motion, direction = pose_result
    return jsonify({
        "yaw": round(yaw, 2),
        "pitch": round(pitch, 2),
        "roll": round(roll, 2),
        "motion": round(motion, 2),
        "direction": direction
    })

@app.route("/register", methods=["POST"])
def register():
    ma_nv = request.form.get("ma_nv")
    ten_nv = request.form.get("ten_nv")
    if not ma_nv or not ten_nv:
        return jsonify({"error": "Thiếu thông tin nhân viên"}), 400

    embeddings = []
    saved_files = []

    for i in range(5):
        file_key = f"image_{i}"
        if file_key not in request.files:
            return jsonify({"error": f"Thiếu ảnh {i+1}"}), 400

        file = request.files[file_key]
        img_bytes = file.read()

        emb = engine.extract_embedding(img_bytes)
        if emb is None:
            return jsonify({"error": f"Không phát hiện khuôn mặt ở ảnh {i+1}"}), 400

        emb = normalize(emb)
        embeddings.append(emb.tolist())

        filename = f"{ma_nv}_{i+1}_{int(time.time())}.jpg"
        filepath = os.path.join(DATA_DIR, filename)
        with open(filepath, "wb") as f:
            f.write(img_bytes)
        saved_files.append(filename)

    json_path = os.path.join(DATA_DIR, f"{ma_nv}_embedding_{int(time.time())}.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump({
            "ma_nv": ma_nv,
            "ten_nv": ten_nv,
            "embeddings": embeddings,
            "files": saved_files
        }, f, ensure_ascii=False, indent=2)

    return jsonify({
        "thong_bao": f"Đăng ký nhân viên {ten_nv} thành công!",
        "ma_nv": ma_nv,
        "ten_nv": ten_nv,
        "so_anh": len(saved_files)
    })

@app.route("/checkin", methods=["POST"])
def checkin():
    if "image" not in request.files:
        return jsonify({"error": "Thiếu ảnh để nhận diện"}), 400

    img_bytes = request.files["image"].read()
    emb = engine.extract_embedding(img_bytes)
    if emb is None:
        return jsonify({"error": "Không phát hiện khuôn mặt"}), 400

    emb = normalize(emb)

    best_match = None
    best_score = float("inf")
    count_json = 0

    for file_name in os.listdir(DATA_DIR):
        if not file_name.endswith(".json"):
            continue
        count_json += 1
        json_path = os.path.join(DATA_DIR, file_name)
        with open(json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
            for stored in data["embeddings"]:
                dist = cosine(emb, normalize(stored))
                if dist < best_score:
                    best_score = dist
                    best_match = data

    similarity = max(0.0, min(100.0, (1 - min(best_score / THRESHOLD, 1)) * 100))

    if best_match and best_score < THRESHOLD:
        status, session, check_in_str, check_out_str = record_attendance(
            best_match["ma_nv"], best_match["ten_nv"]
        )

        if status == "in":
            message = f"Đã ghi nhận giờ vào (lần {session})"
            action = "check_in"
        else:
            message = f"Đã ghi nhận giờ ra (lần {session})"
            action = "check_out"

        return jsonify({
            "thong_bao": f"{message} ({similarity:.1f}%)",
            "ma_nv": best_match["ma_nv"],
            "ten_nv": best_match["ten_nv"],
            "do_giong": f"{similarity:.1f}%",
            "score": round(float(best_score), 4),
            "action": action,
            "session": session,
            "gio_vao": check_in_str,
            "gio_ra": check_out_str
        })

    return jsonify({
        "thong_bao": "Không khớp với nhân viên nào",
        "do_giong": f"{similarity:.1f}%",
        "score": round(float(best_score), 4)
    })

@app.route("/report", methods=["GET"])
def report():
    from_date = request.args.get("from")
    to_date = request.args.get("to")

    if not from_date or not to_date:
        return jsonify({"error": "Thiếu tham số from / to"}), 400

    try:
        from_dt = datetime.strptime(from_date, "%Y-%m-%d").date()
        to_dt = datetime.strptime(to_date, "%Y-%m-%d").date()
    except ValueError:
        return jsonify({"error": "Sai định dạng ngày (YYYY-MM-DD)"}), 400

    docs = attendance_col.find({
        "ngay": {"$gte": from_dt.isoformat(), "$lte": to_dt.isoformat()}
    }).sort([("ngay", 1), ("session", 1), ("ma_nv", 1)])

    result = []
    for doc in docs:
        result.append({
            "ma_nv": doc["ma_nv"],
            "ten_nv": doc["ten_nv"],
            "ngay": doc["ngay"],
            "lan": doc.get("session", 1),
            "gio_vao": doc.get("check_in", ""),
            "gio_ra": doc.get("check_out", "")
        })

    return jsonify(result)

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
import os
import time
import json
from flask import Flask, request, jsonify
from flask_cors import CORS
from face_engine import FaceEngine
import numpy as np
from scipy.spatial.distance import cosine

app = Flask(__name__)
CORS(app)

engine = FaceEngine()
DATA_DIR = "data"
os.makedirs(DATA_DIR, exist_ok=True)

@app.route("/")
def home():
    return jsonify({"message": "Server Flask đang chạy thành công!"})

# ------------------- ĐĂNG KÝ NHÂN VIÊN -------------------
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

        embedding = engine.extract_embedding(img_bytes)
        if embedding is None:
            return jsonify({"error": f"Không phát hiện khuôn mặt ở ảnh {i+1}"}), 400

        # Chuyển numpy array thành list để lưu JSON
        if isinstance(embedding, np.ndarray):
            embedding = embedding.tolist()
        embeddings.append(embedding)

        # Lưu file ảnh
        timestamp = int(time.time())
        filename = f"{ma_nv}_{i+1}_{timestamp}.jpg"
        filepath = os.path.join(DATA_DIR, filename)
        with open(filepath, "wb") as f:
            f.write(img_bytes)
        saved_files.append(filename)

    # Ghi embedding ra file JSON (UTF-8)
    json_path = os.path.join(DATA_DIR, f"{ma_nv}_embedding_{int(time.time())}.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump({
            "ma_nv": ma_nv,
            "ten_nv": ten_nv,
            "embeddings": embeddings,
            "files": saved_files
        }, f, ensure_ascii=False, indent=2)

    return app.response_class(
        response=json.dumps({
            "thong_bao": f"Đăng ký nhân viên {ten_nv} thành công!",
            "ma_nv": ma_nv,
            "ten_nv": ten_nv,
            "so_anh": len(saved_files)
        }, ensure_ascii=False, indent=2),
        mimetype="application/json"
    )

# ------------------- NHẬN DIỆN KHUÔN MẶT -------------------
@app.route("/checkin", methods=["POST"])
def checkin():
    if "image" not in request.files:
        return jsonify({"error": "Thiếu ảnh để nhận diện"}), 400

    file = request.files["image"]
    img_bytes = file.read()

    embedding = engine.extract_embedding(img_bytes)
    if embedding is None:
        return jsonify({"error": "Không phát hiện khuôn mặt"}), 400

    if isinstance(embedding, np.ndarray):
        embedding = embedding.tolist()

    best_match = None
    best_score = 1.0  # cosine distance, càng nhỏ càng giống

    # Duyệt qua các nhân viên đã đăng ký
    for file_name in os.listdir(DATA_DIR):
        if not file_name.endswith(".json"):
            continue
        json_path = os.path.join(DATA_DIR, file_name)
        with open(json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
            for emb in data["embeddings"]:
                dist = cosine(embedding, emb)
                if dist < best_score:
                    best_score = dist
                    best_match = data

    # Tính phần trăm độ giống (0.6 = ngưỡng 0%)
    similarity = max(0, min(100, (1 - min(best_score / 0.6, 1)) * 100))

    # Nếu khớp
    if best_match and best_score < 0.6:
        return app.response_class(
            response=json.dumps({
                "thong_bao": f"Nhận diện thành công ({similarity:.1f}%)",
                "ma_nv": best_match["ma_nv"],
                "ten_nv": best_match["ten_nv"],
                "do_giong": f"{similarity:.1f}%",
                "score": round(float(best_score), 4)
            }, ensure_ascii=False, indent=2),
            mimetype="application/json"
        )
    else:
        return app.response_class(
            response=json.dumps({
                "thong_bao": f"Không khớp với nhân viên nào ({similarity:.1f}%)",
                "score": round(float(best_score), 4),
                "do_giong": f"{similarity:.1f}%"
            }, ensure_ascii=False, indent=2),
            mimetype="application/json"
        )

# ------------------- CHẠY SERVER -------------------
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)

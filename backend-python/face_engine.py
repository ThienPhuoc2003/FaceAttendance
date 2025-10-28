import insightface
import cv2
import numpy as np

class FaceEngine:
    def __init__(self):
        print("[INFO] Loading InsightFace model...")
        self.model = insightface.app.FaceAnalysis(providers=['CPUExecutionProvider'])
        self.model.prepare(ctx_id=0)
        print("[INFO] Model loaded successfully!")

    def extract_embedding(self, image_bytes):
        np_arr = np.frombuffer(image_bytes, np.uint8)
        img = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)

        faces = self.model.get(img)
        if len(faces) == 0:
            return None

        # Lấy embedding của khuôn mặt đầu tiên
        return faces[0].embedding.tolist()

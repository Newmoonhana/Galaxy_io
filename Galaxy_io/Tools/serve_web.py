"""
웹 빌드 테스트용 http 서버.

멀티스레드(SharedArrayBuffer)에 필요한 COOP/COEP 헤더를 붙이고, Gzip 빌드 파일을 브라우저가 풀 수 있게 서빙함.
브라우저는 localhost 에서만 http 멀티스레드를 허용함. 다른 PC 에서 접속하면 싱글스레드 빌드를 쓸 것.

사용법: python serve_web.py <빌드 폴더> [포트=8000]
예시:   python Tools/serve_web.py Builds/Web/MT
"""
import http.server
import os
import sys

# 압축을 벗긴 뒤의 실제 타입.
CONTENT_TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".data": "application/octet-stream",
    ".json": "application/json",
}


class Handler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".gz"):
            path = path[:-3]
        for ext, content_type in CONTENT_TYPES.items():
            if path.endswith(ext):
                return content_type
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split("?")[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cross-Origin-Resource-Policy", "cross-origin")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    root = sys.argv[1]
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8000
    os.chdir(root)

    server = http.server.ThreadingHTTPServer(("0.0.0.0", port), Handler)
    print(f"Serving {os.getcwd()} at http://localhost:{port}")
    server.serve_forever()

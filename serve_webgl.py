"""Local web server for running the WebGL build outside Unity.

Serves the ./Build folder next to this script, sending the gzip
Content-Encoding header that Unity WebGL builds need, then opens it
in the browser.

Run:  python serve_webgl.py      (or double-click run_webgl.bat)
Stop: Ctrl+C
"""
import functools
import http.server
import os
import socketserver
import threading
import webbrowser

PORT = 8000
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Build")


class UnityHandler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".gz"):
            inner = path[:-3]
            if inner.endswith(".wasm"):
                return "application/wasm"
            if inner.endswith(".js"):
                return "application/javascript"
            return "application/octet-stream"
        if path.endswith(".wasm"):
            return "application/wasm"
        return super().guess_type(path)

    def end_headers(self):
        if self.path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        super().end_headers()


class Server(socketserver.TCPServer):
    allow_reuse_address = True


def main():
    if not os.path.isfile(os.path.join(ROOT, "index.html")):
        print(f"Could not find index.html in: {ROOT}")
        print("Build the WebGL game into that folder first, or edit ROOT in this script.")
        input("Press Enter to close...")
        return

    url = f"http://127.0.0.1:{PORT}"
    handler = functools.partial(UnityHandler, directory=ROOT)
    with Server(("127.0.0.1", PORT), handler) as httpd:
        print(f"Serving {ROOT}")
        print(f"Open {url}  (press Ctrl+C to stop)")
        threading.Timer(1.0, lambda: webbrowser.open(url)).start()
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nStopped.")


if __name__ == "__main__":
    main()

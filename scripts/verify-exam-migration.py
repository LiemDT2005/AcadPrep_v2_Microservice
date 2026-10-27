#!/usr/bin/env python3
"""Run the exam lifecycle smoke test with isolated SQLite DBs and local ports.
Build the solution first. Does not require Docker, Identity, Redis or RabbitMQ.
"""
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
SERVICES = [
    ("Content", "src/Services/Content", 25006, {"Services__ExamApi": "http://127.0.0.1:25003"}),
    ("Exam", "src/Services/Exam", 25003, {"Content__BaseUrl": "http://127.0.0.1:25006/"}),
    ("ApiGateway", "src/ApiGateways/ApiGateway", 25080, {
        "ReverseProxy__Clusters__exam__Destinations__d1__Address": "http://127.0.0.1:25003/",
        "ReverseProxy__Clusters__content__Destinations__d1__Address": "http://127.0.0.1:25006/"}),
    ("Storefront.Web", "src/Web/Storefront.Web", 25100, {"GatewayUrl": "http://127.0.0.1:25080"}),
]


def main():
    processes = []
    logs = []
    with tempfile.TemporaryDirectory(prefix="acadprep-migration-") as directory:
        try:
            for name, project, port, config in SERVICES:
                env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", Urls=f"http://127.0.0.1:{port}",
                           DOTNET_CLI_TELEMETRY_OPTOUT="1", **config)
                env["ConnectionStrings__Default"] = f"Data Source={directory}/{name}.db"
                env["ConnectionStrings__Sqlite"] = env["ConnectionStrings__Default"]
                log = open(Path(directory) / f"{name}.log", "w+")
                logs.append((name, log))
                process = subprocess.Popen(["dotnet", f"bin/Debug/net9.0/{name}.dll"],
                                           cwd=ROOT / project, env=env, stdout=log, stderr=subprocess.STDOUT)
                processes.append(process)
                ready = False
                for _ in range(120):
                    if process.poll() is not None:
                        break
                    try:
                        with urllib.request.urlopen(f"http://127.0.0.1:{port}/health" if name != "Storefront.Web"
                                                    else f"http://127.0.0.1:{port}/", timeout=1) as response:
                            ready = response.status == 200
                        if ready:
                            break
                    except Exception:
                        time.sleep(0.25)
                if not ready:
                    raise RuntimeError(f"{name} did not start")
            subprocess.run([sys.executable, str(ROOT / "scripts/smoke-exam-sessions.py"),
                            "http://127.0.0.1:25080", "http://127.0.0.1:25100"], check=True)
        except Exception:
            for name, log in logs:
                log.flush()
                log.seek(0)
                print(f"{name}:\n{log.read()[-5000:]}", file=sys.stderr)
            raise
        finally:
            for process in processes:
                process.terminate()
            for process in processes:
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            for _, log in logs:
                log.close()


if __name__ == "__main__":
    main()

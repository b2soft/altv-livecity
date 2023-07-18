import shutil
import os
import pathlib
import subprocess

serverPath = pathlib.Path(os.getenv("ALTV_SERVER_ROOT")) / "resources/livecity-server"

repoFolder = pathlib.Path(os.path.dirname(os.path.realpath(__file__))).parent
distPath = repoFolder / "dist/livecity-server"

subprocess.run(
    [
        "dotnet",
        "publish",
        repoFolder / "Server/LiveCity.Server.csproj",
        "-c",
        "DebugLocal",
        "--no-build",
    ],
    shell=True,
)
print("[LiveCity Builder] Copying Server Started")

src = repoFolder / "Server/build/DebugLocal/publish"

dst = distPath

print("[LiveCity Builder] Deleting destination resource folder")
if dst.exists():
    shutil.rmtree(dst)

shutil.copytree(src, dst)

print("[LiveCity Builder] Copying resource.toml")
shutil.copyfile(
    repoFolder / "Server/resource.toml", pathlib.Path(dst) / "resource.toml"
)


print("[LiveCity Builder] Deploying Server to Local")

print("[LiveCity Builder] Deleting destination resource folder")
if serverPath.exists():
    shutil.rmtree(serverPath)
shutil.copytree(distPath, serverPath)
print("[LiveCity Builder] Deployed Server to Local")

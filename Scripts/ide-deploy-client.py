import shutil
import os
import pathlib
import subprocess

serverPath = pathlib.Path(os.getenv("ALTV_SERVER_ROOT")) / "resources/livecity-client"

repoFolder = pathlib.Path(os.path.dirname(os.path.realpath(__file__))).parent
distPath = repoFolder / "dist/livecity-client"

subprocess.run(
    [
        "dotnet",
        "publish",
        repoFolder / "Client/LiveCity.Client.csproj",
        "-c",
        "DebugLocal",
        "--no-build",
    ],
    shell=True,
)
print("[LiveCity Builder] Copying Client Started")

src = repoFolder / "Client/build/DebugLocal/publish"

dst = distPath

print("[LiveCity Builder] Deleting destination resource folder")
if dst.exists():
    shutil.rmtree(dst)

shutil.copytree(src, dst)

print("[LiveCity Builder] Copying resource.toml")
shutil.copyfile(
    repoFolder / "Client/resource.toml", pathlib.Path(dst) / "resource.toml"
)


print("[LiveCity Builder] Deploying Client to Local")

print("[LiveCity Builder] Deleting destination resource folder")
if serverPath.exists():
    shutil.rmtree(serverPath)
shutil.copytree(distPath, serverPath)
print("[LiveCity Builder] Deployed Client to Local")

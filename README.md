#  Mako-IoT.Device.Services.Server
WebServer implementation based on [nanoFramework.WebServer](https://github.com/nanoframework/nanoFramework.WebServer) with additional features:
- route parameters
- optimized route resolving logic
- improved API/MVC controller initialization

## How to manually sync fork
- Clone repository and navigate into folder
- From command line execute bellow commands
- **git remote add upstream https://github.com/CShark-Hub/Mako-IoT.Base.git**
- **git fetch upstream**
- **git rebase upstream/main**
- If there are any conflicts, resolve them
  - After run **git rebase --continue**
  - Check for conflicts again
- **git push -f origin main**

# altv-livecity
Traffic synchronization proof-of-concept alt:V Multiplayer.

# What is this?
This is small proof-of-concept for spawning & syncing traffic in GTAO-like way using alt:V dev branch. Last working server version is `15.0-dev368 (dev)`. I made some research, how GTAO and GTA SP handles traffic. After I made some decent results, I stopped the devlopment, because of limitations from alt:V. That's why I want to share this repository for everyone. You might take some parts from it, if you want to.

This repository will be updated only to keep it working. Maybe.

If you have any questions - please use github Discussions page or reach me in alt:V Discord, `@b2soft`. I don't care bugs and/or, so just don't. Your PR will be never accepted. Maybe.

# Data I discovered so far
There is a lot of needed data you can find in Data folder.
Partially it was copied from https://github.com/KWMSources/pedSyncer

The other part is modified DurtyFree dumps https://github.com/DurtyFree/gta-v-data-dumps

CarGenerators is my file generated using CodeWalker.

## Breakdown of files used
File | Description | Details
---- | ----------- | -------
footpath.msgpack | Contains navigation mesh info | Based on DurtyFree dump, cleaned to have only needed navmesh parts, where ped can be spawned (sidewalks only)
AllowedScenarios.json | Contains allowed scenarios for Scenario Ped | From pedSyncer repo
CarColorsNum.json | Contains Car Colors | Copy from pedSyncer repo
CarGenerators.xml | Contains all the Car Generators | Generated from CodeWalker, used for Parked Vehicles
CarModels.json | Contains all the Car Models | Copy from pedSyncer repo
ColorlessCars.json | Contains car models, which are not "painted" | Red police car looks strange, ya?
ExtendedNodes.json | Contiains all Path Nodes for cars/boats | Based on DurtyFree dump, added best headings provided by GTA natives (did not help) + minified format
pedComponentVariations.json | Contains variations of Drawable components for Ped models | From DurtyFree dump
PedModelGroup.json | Contains groups of ped models used by Scenario Peds | From pedSyncer repo
PopCycle | Contains Population Cycles description | Copy-Paste from GTA files
PopGroups.xml | Contains relations between Population Cycle group and Ped/Vehicle model | Detailed description can be found below
ScenarioPoints.json | Contains all Scenario points for Scenario Peds | From pedSyncer repo
vehicles.json | Contains Vehicle metadata | From DurtyFree dump
ZoneBind.ymt | Contains relations between Zone and PopZone names | Detailed description can be found below
Zones | Contains bounds of Zones | Detailed description can be found below

## Population Cycle format
Everything related to spawn in GTA follows the Population Cycles file. Each section (between `POP_SCHEDULE:` and `END_POP_SCHEDULE`) of PopCycle file represents one PopZone. SP and MP zones are set up separately. Each line of a section represents 2 hours time period, starting from 00:00. Each line contains Limits and Probabilities.

Let's break down one of lines, for instance `NET_VINE_WOOD` PopZone, 12:00-14:00 time period:

`10     50        85      20         30           1           0           3          4                   1           peds  West_Vinewood_Hipsters 20  West_Vinewood_StreetGeneral 60  West_Vinewood_Vinewood 10  Club_Nighttime 00  cars  VEH_RICH_MP 40  VEH_MID_MP 45  VEH_TRANSPORT_MP 05  VEH_LARGE_CITY_MP 00  VEH_BIKES_MP 10`
Format of Limits is:
- 10 - Maximum number of ambient peds
- 50 - Maximum number of scenario peds
- 85 - Maximum number of cars
- 20 - Maximum number of parked cards
- 30 - Maximum number of low priority parked cards - I do not know what does this mean. Probably GTA will destream low priority parked cars to let closer vehicle to spawn
- 1 - Percentage of police cars
- 0 - Percentage of peds, that are cops
- 3 - Maximum number of ped models, to be used by scenario peds
- 4 - Maximum number of vehicle models, to be used by scenario vehicles
- 1 - MaxPreAssignedParked - I do not know what does this mean. Probably, how much parked vehicles GTA pre-spawns (and other are just streamed on demand)

Format of probabilities is:
- `peds` - start of Peds probabilities
- `West_Vinewood_Hipsters` - name of PopGroup to be used
- 20 - Probability in percents. 20 % that spawned ped will be from `West_Vinewood_Hipsters` group
- (same formatted groups and probabilities till cars section)
- `cars` - start of Cars probabilities
- `VEH_RICH_MP` - name of PopGroup to be used (same logic as for peds)
- 40 - Probability in percents. 20 % that spawned vehicle will be from `VEH_RICH_MP` group
- (same formatted groups and probabilities till end of line)

To match the exact ped or vehicle model `PopGroups.xml` is used: you can find lists of corresponding models for each PopCycle group.

### Zones drama and how spawn works
You may be noticed, that name of PopZone from PopCycle file does not equal any known zone names from natives etc. Imagine we want to spawn a ped at given coordinate. To match the zone, following steps are required:
1. Based on a given position, find the Zone using Zones.txt file, for instance our position belongs to `Z_Alta3` Zone
2. Find PopZone using ZoneBind.ymt, for `Z_Alta3` you will have 2 names `alta` and `net_vine_wood`. The former is used for GTA SP, the latter - for GTAO
3. Taking `net_vine_wood` find the corresponding PopZone in `PopCycle` file.
4. Based on current time, select line (same as we took for Population Cycle format):
``10     50        85      20         30           1           0           3          4                   1           peds  West_Vinewood_Hipsters 20  West_Vinewood_StreetGeneral 60  West_Vinewood_Vinewood 10  Club_Nighttime 00  cars  VEH_RICH_MP 40  VEH_MID_MP 45  VEH_TRANSPORT_MP 05  VEH_LARGE_CITY_MP 00  VEH_BIKES_MP 10``
5. Select weightned random distributed PopGroup, for instance, `West_Vinewood_Hipsters`
6. Select random ped model using `PopGroups.xml` file, for instance `a_f_y_hipster_01`
7. Spawn

## Code
Copy the Data folder content to `[altv-server-root]/data`. Project uses C# for server and client. Open `LiveCity.sln` with Visual Studio. If you have `ALTV_SERVER_ROOT` environment var pointing to the altv server root, you can choose DebugLocal configuration.

Data is loaded in `Server/Navigation/NavigationMeshProvider.cs` and `Server/LiveCity/LiveCityDataService.cs`.

Main logic can be found in `Server/LiveCity/LiveCityService.cs` and `Client/Resource/LiveCity/LiveCityService.cs`.

# How does it work in LiveCity
## What did I find in GTAO?
I found that GTAO spawns traffic within 600 meters around the player and extensively stream it in and out based on camera angle + cache "hidden" entities. After 600m distance GTA uses billboards, probably native.

Cars have no peds driving, unless car is within 80m radius of player + when you zoom in using scope, peds are shown on demand. Helicopters have drivers at all times.

## How is it done in this repo?
### Main Algorithm
There are 4 types of NPCs spawned:
- Wander Peds - spawned randomly on a sidewlk, just wandering around
- Scenario Peds - spawned at Scenario Points taking into account allowed scenarios (like smoking, drinking coffee etc)
- Wander Vehicles - spawned randomly at eligible Street Nodes. Vehicle's door are locked, drivers follow the safe driving style, obey the rules
- Parked Vehicles - spawned at Car Generators positions taking into account rules from GTA

When player connects to server, it's being added to list of players to track. After player spawn is complete, each 100 ms LiveCityService tries to fill the NPCs within two ranges, considering streaming range is default (400 m):
- Close range - circle zone around the player, default radius is 200 m
- Far range - a sector of ring zone around the player, min radius 200m, max radius 400 m, sector angle is 90 degrees default, main direction is player orientation (tried several times with camera forward vector instead, without caching looks worse, than entity rotation, imo)

Entities are never spawned closer than 100 m default. First it counts how much entities are around the player and if it's possible to add new entities. Budgets are set to not overshoot hard limits of GTA (like 128 peds maximum). Budgets and ranges are specified at `GlobalConfig.cs`.

### How model is chosen
Based on different data dumps, these rules are applied.
1. Wander Ped models depend on Population Cycle groups and position of spawn
2. Scenario Ped models depend on allowed models for specific scenarios + based on a Population Cycle as a backup, if scenario is not limited to specific models
3. Wander Vehicles models depend on Population Cycle groups and position of spawn. Drivers for Wander Vehicles are chosen the same way as Wander Peds
4. Parked Vehicles models depend on Car Generators' limitations + based on a Population Cycle as a backup, if car generator does not specify exact models

# Known Issues and Limits
1. No caching. Entities are destroyed instead of cache them.
2. No driverless vehicles. While it can give some room to have more vehicles, it implies for fine control over creating Wander Vehicles.
3. GetAll* used, becuase GetAll*InRange don't work
4. Wander Vehicles are not reacting to Emergency vehicle sirens, like police. Probably, becuase alt:V creates peds as Mission type, and not ambient. I tried different natives to fix this, but it's not possible to change Ped type to Ambient
5. No Billboards/Fake vehicles. Native is blocked by alt:V, no workaround provided for now.
6. 128 entities is a hardcoded limit of alt:V. LiveCity is adjusted to have 100 Entities around (+ 1 player + 27 other players room). Wander vehicle costs 2 Entities (Vehicle + Driver)
7. No 2+ ped scenarios. Right now only one ped is playing scenario. GTA has 2 people talking mext to each other, for example.
8. No Vehicle scenarios. GTA has scenarios to vehicles parking, or going to drive-by Clickin' Bell. Not supporte in LiveCity.

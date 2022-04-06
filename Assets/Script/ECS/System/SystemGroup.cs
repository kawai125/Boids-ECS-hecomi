using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;


[UpdateBefore(typeof(MoveSystemGroup))]
public class ManagerSystemGroup : ComponentSystemGroup { }

[UpdateBefore(typeof(BuildCellIndexSystemGroup))]
public class MoveSystemGroup : ComponentSystemGroup { }

[UpdateBefore(typeof(NeighborDetectionSystemGroup))]
public class BuildCellIndexSystemGroup : ComponentSystemGroup { }

[UpdateBefore(typeof(BoidSystemGroup))]
public class NeighborDetectionSystemGroup : ComponentSystemGroup { }

[UpdateInGroup(typeof(SimulationSystemGroup))]
public class BoidSystemGroup : ComponentSystemGroup { }

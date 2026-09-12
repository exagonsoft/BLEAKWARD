using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingTypeListSO", menuName = "Scriptable Objects/BuildingTypeList")]
public class BuildingTypeListSO : ScriptableObject
{
    public List<BuildingTypeSO> list;
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceTypeListSO", menuName = "Scriptable Objects/ResourceTypeList")]
public class ResourceTypeListSO : ScriptableObject
{
    public List<ResourceTypeSO> list;
}

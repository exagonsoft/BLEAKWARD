using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneDetailListSO", menuName = "Scriptable Objects/Scene Detail List")]
public class SceneDetailListSO : ScriptableObject
{
    public List<SceneDetailSO> list;
}

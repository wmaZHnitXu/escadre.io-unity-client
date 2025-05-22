using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityLoggerDumper : MonoBehaviour
{
    public static UnityLoggerDumper Instance;
    [TextArea]
    [SerializeField] public string LogDump;

    void Awake()
    {
        Instance = this;
    }

    public void Add(string additionalLine)
    {
        LogDump += additionalLine + '\n';
    }
}

using System;
using UnityEngine;

[Serializable]
public class Edge
{
    public Node targetNode;
    public float resistance = 1.0f;
    public float flowRate = 0.0f;

    public Edge(Node target, float res = 1.0f)
    {
        targetNode = target;
        resistance = res;
    }
}
using System.Collections.Generic;
using UnityEngine;
using HandyUtilities;
using System;
using System.Collections;

[System.Serializable]
public class Bezier
{
    public AnimationCurve evaluationCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField]
    [HideInInspector]
    Vector3[] m_handles = new Vector3[4];
    public int stepCount = 10;
    public Vector3[] handles { get { return m_handles; } }
    public Type type;
    public bool smooth = false;
    public bool useEvaluationCurve = false;

    public Vector3 startHandle
    {
        get { return m_handles[0]; }
        set { m_handles[0] = value; }
    }

    public Vector3 endHandle
    {
        get { return m_handles[m_handles.Length - 1];  }
        set { m_handles[m_handles.Length - 1] = value; }
    }

    public Bezier()
    {
        m_handles[0] = new Vector3(-.5f, -5f);
        m_handles[1] = new Vector3(.5f, 5f);
        type = Type.Linear;
    }

    public Bezier(Vector3 p0, Vector3 p1)
    {
        m_handles[0] = p0;
        m_handles[1] = p1;
        type = Type.Linear;
    }

    public Bezier(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        m_handles[0] = p0;
        m_handles[1] = p1;
        m_handles[3] = p2;
        type = Type.Quadratic;
    }

    public Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        m_handles[0] = p0;
        m_handles[1] = p1;
        m_handles[2] = p2;
        m_handles[3] = p3;
        type = Type.Cubic;
    }

    public enum Type
    {
        Linear = 0, Quadratic = 1, Cubic = 2
    }

    public Bezier Split(Vector3 point, out Bezier secondHalf)
    {

        secondHalf = new Bezier();
        return this;
    }

    public Vector3 GetClosestPoint(Vector2 point)
    {
        if (type == Type.Linear)
        {
            return Geometry.GetClosestPointOnLineSegment(m_handles[0], m_handles[3], point);
        }
        Vector3 closest = Vector3.zero;
        float t = 0f;
        var stepSize = 1f / stepCount;
        float d = float.MaxValue;
        for (int i = 0; i < stepCount; i++)
        {
            var v = useEvaluationCurve ? evaluationCurve.Evaluate(t) : t;
            var p = Evaluate(v);
            var vNext = useEvaluationCurve ? evaluationCurve.Evaluate(t += stepSize) : t += stepSize;
            var next = Evaluate(vNext);
            var pointOnSeg = Geometry.GetClosestPointOnLineSegment(p, next, point);
            var dist = Vector3.SqrMagnitude(pointOnSeg - point);
            if(d > dist)
            {
                closest = pointOnSeg;
                d = dist;
            }
        }
        return closest;
    }

    public Vector3 Evaluate(float t)
    {
        if(type == Type.Linear)
        {
            return Vector3.Lerp(m_handles[0], m_handles[3], t);
        }
        else if(type == Type.Quadratic)
        {
            var s1 = Vector3.Lerp(m_handles[0], m_handles[1], t);
            var s2 = Vector3.Lerp(m_handles[1], m_handles[3], t);
            return Vector3.Lerp(s1, s2, t);
        }
        else if (type == Type.Cubic)
        {
            var s1 = Vector3.Lerp(m_handles[0], m_handles[1], t);
            var s2 = Vector3.Lerp(m_handles[1], m_handles[2], t);
            var s3 = Vector3.Lerp(m_handles[2], m_handles[3], t);
            var s4 = Vector3.Lerp(s1, s2, t);
            var s5 = Vector3.Lerp(s2, s3, t);
            return Vector3.Lerp(s4, s5, t);
        }
        return Vector3.zero;
    }


    public  Vector3 this[int index]
    {
        get { return m_handles[index]; }
        set { m_handles[index] = value; }
    }

    public IEnumerable<Vector3> GetPoints()
    {
        float t = 0f;
        if(type == Type.Linear)
        {
            yield return m_handles[0];
            yield return m_handles[3];
        }
        else
        {
            var stepSize = 1f / stepCount;
            for (int i = 0; i < stepCount; i++)
            {
                var v = useEvaluationCurve ? evaluationCurve.Evaluate(t) : t;
                t += stepSize;
                yield return Evaluate(v);
            }
        }
    }

}
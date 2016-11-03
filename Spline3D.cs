using UnityEngine;
using System.Collections.Generic;
using HandyUtilities;

namespace SplineEditor3D
{
    [ExecuteInEditMode]
    public class Spline3D : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        List<Bezier> m_beziers = new List<Bezier>();
        public List<Bezier> beziers { get { return m_beziers; } }
        public bool editPoints { get; set; }
        public bool drawPoints { get; set; }
        Transform m_transform;

        [ReadOnly]
        public float length;

        public virtual void OnSplineChange()
        {
            var l = 0f;
            foreach (var b in beziers)
            {
                var bL = b.GetLenght();
                b.startLength = l;
                l += b.GetLenght();
                b.endLength = l;
            }
            length = l;
        }

        void Awake()
        {
            Init();
        }


        public virtual void Init()
        {
            m_transform = transform;
        }

        public Vector3 Evaluate(float n)
        {
            foreach (var s in beziers)
            {
                var start = s.startLength / length;
                var end = s.endLength / length;
                if (n >= start && n <= end)
                {
                    return s.Evaluate(Helper.Remap(n, start, end, 0f, 1f));
                }
            }
            return Vector3.zero;
        }
     
    }

}

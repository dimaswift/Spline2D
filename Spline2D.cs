using UnityEngine;
using System.Collections.Generic;

namespace SplineEditor2D
{
    [ExecuteInEditMode]
    public class Spline2D : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        List<Bezier> m_beziers = new List<Bezier>();
        public List<Bezier> beziers { get { return m_beziers; } }
        public bool editPoints { get; set; }

        public virtual void OnSplineChange() { }

        public List<Vector3> GetPointsList()
        {
            return GetPointsList(new List<Vector3>());
        }

        public List<Vector3> GetPointsList(List<Vector3> source)
        {
            source.Clear();
            foreach (var p in GetPoints())
            {
                source.Add(p);
            }
            return source;
        }

        void Awake()
        {
            Init();
        }

        public List<Vector2> GetPointsList2D()
        {
            return GetPointsList2D(new List<Vector2>());
        }

        public virtual void Init() { }

        public List<Vector2> GetPointsList2D(List<Vector2> source)
        {
            source.Clear();
            foreach (var p in GetPoints())
            {
                source.Add(p);
            }
            return source;
        }

        public IEnumerable<Vector3> GetPoints()
        {
            for (int i = 0; i < beziers.Count; i++)
            {
                var b = beziers[i];

                foreach (var p in b.GetPoints())
                {
                    yield return p;
                }
             
            }
            yield return beziers[beziers.Count - 1].Evaluate(1);
        }
    }

}

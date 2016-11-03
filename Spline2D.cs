using UnityEngine;
using System.Collections.Generic;

namespace SplineEditor2D
{
    [ExecuteInEditMode]
    public class Spline2D : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        List<Bezier> m_beziers = new List<Bezier>()
        {
            new Bezier(new Vector3(0, 1), new Vector3(0.9510565f, 0.309017f)),
            new Bezier(new Vector3(0.9510565f, 0.309017f), new Vector3(0.5877852f, -0.8090171f)),
            new Bezier(new Vector3(0.5877852f, -0.8090171f), new Vector3(-0.5877854f, -0.8090169f)),
            new Bezier(new Vector3(-0.5877854f, -0.8090169f), new Vector3(-0.9510565f, 0.3090171f)),
            new Bezier(new Vector3(-0.9510565f, 0.3090171f), new Vector3(0, 1))
        };

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
            if(beziers.Count == 0)
            {
                beziers.Add(new Bezier(new Vector3(0, 1), new Vector3(0.9510565f, 0.309017f)));
                beziers.Add(new Bezier(new Vector3(0.9510565f, 0.309017f), new Vector3(0.5877852f, -0.8090171f)));
                beziers.Add(new Bezier(new Vector3(0.5877852f, -0.8090171f), new Vector3(-0.5877854f, -0.8090169f)));
                beziers.Add(new Bezier(new Vector3(-0.5877854f, -0.8090169f), new Vector3(-0.9510565f, 0.3090171f)));
                beziers.Add(new Bezier(new Vector3(-0.9510565f, 0.3090171f), new Vector3(0, 1)));
            }
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
            if (beziers.Count == 0)
                yield break;
            for (int i = 0; i < beziers.Count; i++)
            {
                var b = beziers[i];

                foreach (var p in b.GetPoints())
                {
                    yield return p;
                }
             
            }
           // yield return beziers[beziers.Count - 1].Evaluate(1);
        }
    }

}

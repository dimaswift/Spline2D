namespace SplineEditor3D
{
    using UnityEngine;
    using HandyUtilities;
    using UnityEditor;
    using System.Collections.Generic;

    [CustomEditor(typeof(Spline3D), true)]
    public class Spline3DEditor : Editor
    {
        int editedBezierIndex;
        Spline3D spline { get { return (Spline3D) target; } }
        int draggedPoint;
        bool dragging;
        List<Vector3> tmpList = new List<Vector3>(4);
        Vector3 pressedMousePos, pressedPointPos;
        bool drawing;
        Transform drawTarget;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var editing = spline.editPoints;
            var drawing = spline.drawPoints;
            spline.editPoints = GUILayout.Toggle(spline.editPoints, "Edit","Button");
            spline.drawPoints = GUILayout.Toggle(spline.drawPoints, "Draw", "Button");
            if (editing != spline.editPoints)
            {
                editing = spline.editPoints;

                if (editing)
                {
                    OnEditStarted();
                    spline.drawPoints = false;
                }
                else
                {
                    editedBezierIndex = -1;
                    spline.OnSplineChange();
                    EditorUtility.SetDirty(spline);  
                    HandyEditor.RestoreTool();
                }
            }

            if (drawing != spline.drawPoints)
            {
                drawing = spline.drawPoints;

                if (drawing)
                {
                    OnEditStarted();
                    spline.editPoints = false;
                }
                else
                {
                    editedBezierIndex = -1;
                    spline.OnSplineChange();
                    EditorUtility.SetDirty(spline);
                    HandyEditor.RestoreTool();
                }
            }

            if(drawing)
            {
                m_positionHandle = EditorGUILayout.Vector3Field("Position", m_positionHandle);
                var t = drawTarget;
                drawTarget = EditorGUILayout.ObjectField(drawTarget, typeof(Transform), true) as Transform;
                if (t != drawTarget && drawTarget != null)
                    m_positionHandle = drawTarget.position;
                Repaint();
                if(GUILayout.Button("Add Point"))
                {
                    Undo.RecordObject(spline, "Add Spline Point");
                    AddBezier(m_positionHandle);
                }
            }

            if (editing && editedBezierIndex >= 0 && spline.beziers.Count > 0 && editedBezierIndex < spline.beziers.Count)
            {
                EditorGUI.BeginChangeCheck();
                var editedHandle = spline.beziers[editedBezierIndex];
                Undo.RecordObject(spline, "Bezier");
                editedHandle.type = (Bezier.Type) EditorGUILayout.EnumPopup("Type", editedHandle.type);
                if(editedHandle.type == Bezier.Type.Cubic)
                {
                    editedHandle.smooth = EditorGUILayout.Toggle("Smooth", editedHandle.smooth);
                }

                editedHandle.stepCount = EditorGUILayout.IntField("Step Count", editedHandle.stepCount);
                editedHandle.useEvaluationCurve = EditorGUILayout.Toggle("Use Evaluation Curve", editedHandle.useEvaluationCurve);
                if (editedHandle.useEvaluationCurve)
                    editedHandle.evaluationCurve = EditorGUILayout.CurveField("Evaluation Curve", editedHandle.evaluationCurve);
                if(EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(target);
                }
            }
        }

        void OnEditStarted()
        {
            HandyEditor.HideTools();
        }

        void OnSceneGUI()
        {
            if(spline.drawPoints)
            {
                HandyEditor.FreezeScene();
                OnDrawPoints();
            }
            if (spline.editPoints)
            {
                HandyEditor.FreezeScene();
                EditBeziers(spline.beziers, 10);
                DrawBezierControls();
            }
            for (int i = 0; i < spline.beziers.Count; i++)
            {
                DrawBezier(spline.beziers[i], spline.transform, spline.editPoints && i == editedBezierIndex ? Color.red : Color.green.SetAlpha(.5f));
            }
            SceneView.RepaintAll();
        }

        void OnDrawPoints()
        {
            m_positionHandle = Handles.PositionHandle(m_positionHandle, Quaternion.identity);
        }

        void OnDisable()
        {
            editedBezierIndex = -1;
            HandyEditor.RestoreTool();
            Undo.undoRedoPerformed -= OnUndo;
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnUndo()
        {
            if(spline.editPoints)
            {
                if (editedBezierIndex >= spline.beziers.Count)
                    editedBezierIndex = -1;
                else if(editedBezierIndex >= 0)
                {
                    m_positionHandle = spline.transform.TransformPoint(spline.beziers[editedBezierIndex].handles[draggedPoint]);
                }
            }
            spline.OnSplineChange();
        }

        

        Bezier GetClosestBezier(Vector3 point)
        {
            Bezier closest = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < spline.beziers.Count; i++)
            {
                var bezier = spline.beziers[i];
                var center = spline.transform.TransformPoint(Vector3.Lerp(bezier.startHandle, bezier.endHandle, .5f));
                var d = Vector3.Distance(center, point);
                if(d < closestDist)
                {
                    closestDist = d;
                    closest = bezier;
                }
            }
            return closest;
        }

        Bezier PointCastBezier(Vector3 point)
        {
            for (int i = 0; i < spline.beziers.Count; i++)
            {
                var bezier = spline.beziers[i];
                var center = Geometry.GetCentroid(bezier.handles);
                tmpList.Clear();
                for (int j = 0; j < 4; j++)
                {
                    tmpList.Add(bezier.handles[j]);
                }

                tmpList.Sort((p1, p2) =>
                {
                    var a1 = Helper.EulerToTarget(p1, center);
                    var a2 = Helper.EulerToTarget(p2, center);
                    return a1.CompareTo(a2);
                });
                var isInside = Geometry.IsInside(spline.transform.InverseTransformPoint(point), tmpList);
                if (isInside)
                    return bezier;
            }
            return null;
        }

        Vector3 m_positionHandle;

        void EditBeziers(List<Bezier> bezierList, int steps)
        {
            var ray = HandyEditor.mouseRay;
          
            var splineTransform = ((Spline3D) target).transform;
            //  Handles.SphereCap(0, ray.origin + (ray.direction * 20), Quaternion.identity, 1);


            Undo.RecordObject(spline, "Spline3D Bezier");
            


            bool mouseDown0 = HandyEditor.GetMouseButtonDown(0);
            bool mouseUp0 = HandyEditor.GetMouseButtonUp(0);

            if(Event.current.keyCode == KeyCode.Space && Event.current.type == EventType.KeyDown)
            {
                if(dragging)
                {
                    var b = spline.beziers[editedBezierIndex];
                    Vector3 pos = Vector3.zero;
                    for (int i = 0; i < b.handles.Length; i++)
                    {
                        pos += spline.transform.TransformPoint(b.handles[i]);
                    }

                    HandyEditor.FocusOnPoint(pos / b.handles.Length, (b.handles[0] - b.handles[3]).magnitude / 2);
                }
              
                else
                {
                    Vector3 pos = Vector3.zero;
                    for (int i = 0; i < spline.beziers.Count; i++)
                    {
                        pos += spline.transform.TransformPoint(spline.beziers[i].handles[1]);
                    }

                    HandyEditor.FocusOnPoint(pos / spline.beziers.Count, (spline.beziers[0].handles[1] - spline.beziers.LastItem().handles[1]).magnitude * .5f);
                }
            }
            bool control = Event.current.control;
            bool changedPoint = false;
            if (mouseDown0)
            {
                for (int i = 0; i < spline.beziers.Count; i++)
                {
                    var b = spline.beziers[i];
                    for (int j = 0; j < b.handles.Length; j++)
                    {
                        var h = splineTransform.TransformPoint(b.handles[j]);
                        if (Geometry.DistanceToLine(ray, h) < HandleUtility.GetHandleSize(h) * .2f)
                        {
                            if(control && (j == 0 || j == 3))
                            {
                                spline.beziers.RemoveAt(i);
                                dragging = false;
                                return;
                            }
                            editedBezierIndex = i;
                            draggedPoint = j;
                            dragging = true;
                            Handles.SphereCap(0, h, Quaternion.identity, HandleUtility.GetHandleSize(h) * .2f);
                            if (mouseDown0)
                                m_positionHandle = h;
                            changedPoint = true;
                            break;
                        }
                    }
                }

                if(!Event.current.alt 
                    && dragging
                    && editedBezierIndex < spline.beziers.Count 
                    && draggedPoint >= 0 
                    && draggedPoint < 4 
                    && editedBezierIndex >= 0)
                {
                    var h = splineTransform.TransformPoint(spline.beziers[editedBezierIndex].handles[draggedPoint]);
                    if (!changedPoint && Geometry.DistanceToLine(ray, h) > HandleUtility.GetHandleSize(h) * 1.5f)
                    {
                        dragging = false;
                    }
            
                }
            }
     
            //if (draggedPoint >= 0 && editedBezierIndex >= 0 && draggedPoint < 4 && editedBezierIndex < spline.beziers.Count)
            //{
            //    m_positionHandle = Handles.PositionHandle(m_positionHandle, Quaternion.identity);
            //    spline.beziers[editedBezierIndex].handles[draggedPoint] = splineTransform.InverseTransformPoint(m_positionHandle);
            //}

            bool mouseUp = HandyEditor.GetMouseButtonUp(0);

       

            //if (HandyEditor.GetMouseButtonDown(0))
            //{
            //    for (int i = 0; i < bezierList.Count; i++)
            //    {
            //        var bezier = bezierList[i];


            //        for (int j = 0; j < 4; j++)
            //        {
            //            var p = splineTransform.TransformPoint(bezier[j]);
            //            var d = Vector3.Distance(m_positionHandle, p);
            //            if (bezier.type == Bezier.Type.Linear && j != 0 && j != 3)
            //                continue;
            //            if (bezier.type == Bezier.Type.Quadratic && j == 2)
            //                continue;
            //            if (d < 1)
            //            {
            //                if (control)
            //                {
            //                    spline.beziers.RemoveAt(i);
            //                    return;
            //                }
            //                break;
            //            }
            //        }

            //    }
            //}

            //if(!isMouseOverPoint && !dragging)
            //{
            //    var closestBezier = PointCastBezier(m_positionHandle);
            //    if (closestBezier != null)
            //    {
            //        Handles.color = Color.green;
            //        var pointOnSegment = closestBezier.GetClosestPoint(spline.transform.InverseTransformPoint(m_positionHandle));
            //        var worldPointOnSegment = spline.transform.TransformPoint(pointOnSegment);

            //        var ditanceToMouse = Vector3.Magnitude(m_positionHandle - worldPointOnSegment);
            //        if (ditanceToMouse < HandleUtility.GetHandleSize(m_positionHandle) * .3f)
            //        {
            //            Handles.DrawSolidDisc(worldPointOnSegment, Vector3.forward, HandleUtility.GetHandleSize(spline.transform.position) * .05f);
            //            if (HandyEditor.GetMouseButtonDown(0))
            //            {
            //                var newBezier = new Bezier(closestBezier[0], closestBezier[1], closestBezier[2], pointOnSegment);
            //                closestBezier[0] = pointOnSegment;
            //                closestBezier[2] = Vector3.Lerp(closestBezier[2], closestBezier[3], .25f);

            //                newBezier[1] = Vector3.Lerp(newBezier[1], newBezier[0], .5f);
            //                newBezier[2] = Vector3.Lerp(pointOnSegment, newBezier[1], .25f);
            //                var index = spline.beziers.FindIndex(b => b == closestBezier);
            //                closestBezier[1] = Vector3.Lerp(closestBezier[0], closestBezier[2], .25f);
            //                spline.beziers.Insert(index, newBezier);
            //                draggedPoint = 3;
            //                dragging = true;
            //                pressedMousePos = m_positionHandle;
            //                pressedPointPos = worldPointOnSegment;
            //                editedBezierIndex = index;
            //                isMouseOverPoint = true;
            //                dragging = true;
            //            }
            //        }
            //    }
            //}

            if (dragging && editedBezierIndex >= 0 && draggedPoint >= 0)
            {

                var editedBezier = bezierList[editedBezierIndex];
                
                var dragDelta = editedBezier[draggedPoint] - splineTransform.InverseTransformPoint(m_positionHandle);
            
                m_positionHandle = Handles.PositionHandle(m_positionHandle, Quaternion.identity);
                var handleSize = HandleUtility.GetHandleSize(m_positionHandle) * .2f;
                spline.beziers[editedBezierIndex].handles[draggedPoint] = splineTransform.InverseTransformPoint(m_positionHandle);
         
                Vector3 dif = Vector3.zero;

                if (draggedPoint == 0)
                {
                    if (editedBezier.type == Bezier.Type.Cubic && Vector3.Distance(editedBezier[1], editedBezier[draggedPoint]) > handleSize * 2)
                        editedBezier[1] -= dragDelta;
                    if (editedBezierIndex > 0)
                    {
                        var prevBezier = bezierList.PreviousItem(editedBezierIndex);
                        prevBezier[3] = editedBezier[0];
                        if (prevBezier.type == Bezier.Type.Cubic && editedBezier.type == Bezier.Type.Cubic)
                            prevBezier[2] -= dragDelta;

                    }
                }
                else if (draggedPoint == 1)
                {
                    if (editedBezierIndex > 0)
                    {
                        var prevBezier = bezierList.PreviousItem(editedBezierIndex);
                        if (editedBezier.smooth && editedBezier.type == Bezier.Type.Cubic && prevBezier.type == Bezier.Type.Cubic)
                        {
                            dif = editedBezier[1] - editedBezier[0];
                            prevBezier[2] = prevBezier[3] - dif;
                        }
                    }
                }
                else if (draggedPoint == 2)
                {
                    if (editedBezierIndex < bezierList.Count - 1)
                    {
                        var nextBezier = bezierList.NextItem(editedBezierIndex);
                        if (editedBezier.smooth && nextBezier.type == Bezier.Type.Cubic && editedBezier.type == Bezier.Type.Cubic)
                        {
                            dif = editedBezier[3] - editedBezier[2];
                            nextBezier[1] = nextBezier[0] + dif;
                        }
                    }
                }
                else if (draggedPoint == 3)
                {

                    if (editedBezier.type == Bezier.Type.Cubic && Vector3.Distance(editedBezier[2], editedBezier[draggedPoint]) > handleSize * 2)
                        editedBezier[2] -= dragDelta;
                    if (editedBezierIndex < bezierList.Count - 1)
                    {
                        var nextBezier = bezierList.NextItem(editedBezierIndex);
                        nextBezier[0] = editedBezier[3];
                        if (nextBezier.type == Bezier.Type.Cubic && editedBezier.type == Bezier.Type.Cubic)
                            nextBezier[1] -= dragDelta;
                    }
                }
            }

 
            if (mouseUp)
            {
                spline.OnSplineChange();
                EditorUtility.SetDirty(target);
            }
        }

        void AddBezier(Vector3 point)
        {
            if (spline.beziers.Count > 0)
            {
                var localPoint = spline.transform.InverseTransformPoint(point);
                var first = spline.beziers[0];
                var last = spline.beziers.LastItem();
                var firstEndPoint = first[0];
                var lastEndPoint = last[3];
                var distanceToFirst = Vector3.Distance(spline.transform.TransformPoint(firstEndPoint), point);
                var distanceToLast = Vector3.Distance(spline.transform.TransformPoint(lastEndPoint), point);

                if(distanceToFirst < distanceToLast)
                {
                    var p2 = firstEndPoint - (first[1] - firstEndPoint);
                    Bezier newBezier = new Bezier(
                        localPoint,
                        Geometry.MiddlePoint(localPoint, p2),
                        p2,
                        firstEndPoint
                    );
                    newBezier.type = first.type;
                    spline.beziers.Insert(0, newBezier);
                }
                else
                {
                    var p1 = lastEndPoint - (last[2] - lastEndPoint);
                    Bezier newBezier = new Bezier(
                        lastEndPoint,
                        p1,
                        Geometry.MiddlePoint(localPoint, p1),
                        localPoint
                    );
                    newBezier.type = last.type;
                    spline.beziers.Add(newBezier);
                }
            }
            else
            {
                var localPoint = spline.transform.InverseTransformPoint(point);
                var newBezier = new Bezier(
                      localPoint,
                      localPoint + new Vector3(0, .5f, 0),
                      localPoint + new Vector3(.5f, .5f, 0),
                      localPoint + new Vector3(.5f, 0, 0)
                  );
                spline.beziers.Add(newBezier);
            }
            spline.OnSplineChange();
        }

        void DrawBezierControls(bool debug = false)
        {
            var transform = spline.transform;

            if(editedBezierIndex >= 0 && spline.beziers.Count > 0)
            {
                var editedBezier = spline.beziers[editedBezierIndex];
                var h0 = transform.TransformPoint(editedBezier[0]);
                var h3 = transform.TransformPoint(editedBezier[3]);
                Handles.color = Color.green;
                Handles.SphereCap(0, h0, Quaternion.identity, HandleUtility.GetHandleSize(h0) * .1f);
                Handles.SphereCap(0, h3, Quaternion.identity, HandleUtility.GetHandleSize(h3) * .1f);
            }
            
            for (int i = 0; i < spline.beziers.Count; i++)
            {
                var bezier = spline.beziers[i];
               
                var h0 = transform.TransformPoint(bezier[0]);
                var h1 = transform.TransformPoint(bezier[1]);
                var h2 = transform.TransformPoint(bezier[2]);
                var h3 = transform.TransformPoint(bezier[3]);
                Handles.color = Color.gray.SetAlpha(.5f);
                if(debug)
                {
                    var center = transform.TransformPoint(Geometry.GetCentroid(bezier.handles));
                    Handles.Label(center, i.ToString());
                    for (int j = 0; j < 4; j++)
                    {
                        var p = transform.TransformPoint(bezier[j]);
                        Handles.Label(p, j.ToString());
                        Handles.color = Color.grey;
                   
                        Handles.DrawLine(p, transform.TransformPoint(bezier.handles.NextItem(j)));
                    }
                }

                if (bezier.type == Bezier.Type.Cubic)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawLine(h0, h1);
                    Handles.DrawLine(h2, h3);
                    Handles.color = Color.gray.SetAlpha(.5f);
                    Handles.SphereCap(0, h1, Quaternion.identity, HandleUtility.GetHandleSize(h1) * .1f);
                    Handles.SphereCap(0, h2, Quaternion.identity, HandleUtility.GetHandleSize(h2) * .1f);
                }
                else if(bezier.type == Bezier.Type.Quadratic)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawLine(h0, h1);
                    Handles.color = Color.gray.SetAlpha(.5f);
                    Handles.SphereCap(0, h1, Quaternion.identity, HandleUtility.GetHandleSize(h1) * .1f);
                }

               
                var world0 = transform.TransformPoint(bezier[0]);
                var world1 = transform.TransformPoint(bezier[1]);
                var world2 = transform.TransformPoint(bezier[2]);
                var world3 = transform.TransformPoint(bezier[3]);
                Handles.color = Color.blue.SetAlpha(.5f);
                Handles.SphereCap(0, world0, Quaternion.identity, HandleUtility.GetHandleSize(world0) * .1f);
                if (i == spline.beziers.Count - 1)
                {
                    Handles.SphereCap(0, world3, Quaternion.identity, HandleUtility.GetHandleSize(world3) * .1f);
                }
            }
        }


        void DrawBezier(Bezier bezier, Transform splineTransform, Color color, float dotSize = .05f)
        {
            float step = 1f / bezier.stepCount;
            float t = 0f; 
            Handles.color = color;
            var size = HandleUtility.GetHandleSize(splineTransform.position) * dotSize;

            if(bezier.type == Bezier.Type.Linear)
            {
                var start = splineTransform.TransformPoint(bezier.startHandle);
                var end = splineTransform.TransformPoint(bezier.endHandle);
                Handles.DrawLine(start, end);
            }
            else
            {
                for (int i = 0; i < bezier.stepCount; i++)
                {
                    var v = bezier.evaluationCurve.Evaluate(t);
                    var start = splineTransform.TransformPoint(bezier.Evaluate(v));
                    t += step;
                    v = bezier.evaluationCurve.Evaluate(t);
                    var end = splineTransform.TransformPoint(bezier.Evaluate(v));
                    Handles.DrawLine(start, end);
                }
            }
        }
    }
}

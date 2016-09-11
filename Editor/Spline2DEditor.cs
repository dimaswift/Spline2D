namespace SplineEditor2D
{
    using UnityEngine;
    using HandyUtilities;
    using UnityEditor;
    using System.Collections.Generic;

    [CustomEditor(typeof(Spline2D), true)]
    public class Spline2DEditor : Editor
    {
        int editedBezierIndex;
        Spline2D spline { get { return (Spline2D) target; } }
        int draggedPoint;
        bool dragging;
        List<Vector3> tmpList = new List<Vector3>(4);
        Vector3 pressedMousePos, pressedPointPos;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var editing = spline.editPoints;
            
            spline.editPoints = GUILayout.Toggle(spline.editPoints, "Edit Spline","Button");

            if (editing != spline.editPoints)
            {
                editing = spline.editPoints;
                if (editing)
                {
                    OnEditStarted();
                }
                else
                {
                    editedBezierIndex = -1;
                    HandyEditor.RestoreTool();
                }
            }

            if(editing && editedBezierIndex >= 0 && spline.beziers.Count > 0)
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
                    spline.OnSplineChange();
                }
            }
        }

        void OnEditStarted()
        {
            HandyEditor.HideTools();
        }

        void OnSceneGUI()
        {
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
            spline.OnSplineChange();
            if (editedBezierIndex >= spline.beziers.Count)
                editedBezierIndex = -1;
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

        void EditBeziers(List<Bezier> bezierList, int steps)
        {
            var mousePos = HandyEditor.mousePosition;
            var splineTransform = ((Spline2D) target).transform;
            mousePos.z = splineTransform.position.z;
            bool mouse0Down = HandyEditor.GetMouseButtonDown(0);
            bool mouse1Down = HandyEditor.GetMouseButtonDown(1);
            bool mouseUp = HandyEditor.GetMouseButtonUp(0);
            float dragTreshold = HandleUtility.GetHandleSize(mousePos) * .2f;
            bool control = Event.current.control;

            if (mouse0Down || mouse1Down)
            {
                Undo.RecordObject(target, "Spline2D Bezier");
            }

            if (dragging && mouseUp)
            {
                dragging = false;
            }

            if(mouse1Down)
            {
                AddBezier(mousePos);
            }

            bool isMouseOverPoint = false;

            if (mouse0Down)
            {
                for (int i = 0; i < bezierList.Count; i++)
                {
                    var bezier = bezierList[i];


                    for (int j = 0; j < 4; j++)
                    {
                        var p = splineTransform.TransformPoint(bezier[j]);
                        var d = Vector3.Distance(mousePos, p);
                        if (bezier.type == Bezier.Type.Linear && j != 0 && j != 3)
                            continue;
                        if (bezier.type == Bezier.Type.Quadratic && j == 2)
                            continue;
                        if (d < dragTreshold)
                        {
                            if (control)
                            {
                                spline.beziers.RemoveAt(i);
                                return;
                            }
                            draggedPoint = j;
                            pressedMousePos = mousePos;
                            pressedPointPos = p;
                            editedBezierIndex = i;
                            isMouseOverPoint = true;
                            dragging = true;
                            break;
                        }
                    }
                    if (isMouseOverPoint)
                        break;
                }
            }

            if(!isMouseOverPoint && !dragging)
            {
                var closestBezier = PointCastBezier(mousePos);
                if (closestBezier != null)
                {
                    Handles.color = Color.green;
                    var pointOnSegment = closestBezier.GetClosestPoint(spline.transform.InverseTransformPoint(mousePos));
                    var worldPointOnSegment = spline.transform.TransformPoint(pointOnSegment);

                    var ditanceToMouse = Vector3.Magnitude(mousePos - worldPointOnSegment);
                    if (ditanceToMouse < HandleUtility.GetHandleSize(mousePos) * .3f)
                    {
                        Handles.DrawSolidDisc(worldPointOnSegment, Vector3.forward, HandleUtility.GetHandleSize(spline.transform.position) * .05f);
                        if (HandyEditor.GetMouseButtonDown(0))
                        {
                            var newBezier = new Bezier(closestBezier[0], closestBezier[1], closestBezier[2], pointOnSegment);
                            closestBezier[0] = pointOnSegment;
                            closestBezier[2] = Vector3.Lerp(closestBezier[2], closestBezier[3], .25f);
                        
                            newBezier[1] = Vector3.Lerp(newBezier[1], newBezier[0], .5f);
                            newBezier[2] = Vector3.Lerp(pointOnSegment, newBezier[1], .25f);
                            var index = spline.beziers.FindIndex(b => b == closestBezier);
                            closestBezier[1] = Vector3.Lerp(closestBezier[0], closestBezier[2], .25f);
                            spline.beziers.Insert(index, newBezier);
                            draggedPoint = 3;
                            dragging = true;
                            pressedMousePos = mousePos;
                            pressedPointPos = worldPointOnSegment;
                            editedBezierIndex = index;
                            isMouseOverPoint = true;
                            dragging = true;
                        }
                    }
                }
            }

            if (dragging && editedBezierIndex >= 0 && draggedPoint >= 0)
            {

                var editedBezier = bezierList[editedBezierIndex];
            
                var newWorldPoint = mousePos - (pressedMousePos - pressedPointPos);
                var newPoint = splineTransform.InverseTransformPoint(newWorldPoint);
                Handles.color = Color.red;
                var handleSize = HandleUtility.GetHandleSize(newPoint) * .1f;
                Handles.DrawSolidDisc(newWorldPoint, Vector3.forward, handleSize);
                var dragDelta = editedBezier[draggedPoint] - newPoint;
                editedBezier[draggedPoint] -= dragDelta;
             
                Vector3 dif = Vector3.zero;
                if (draggedPoint == 0)
                {
                    if (editedBezier.type == Bezier.Type.Cubic && Vector3.Distance(editedBezier[1], editedBezier[draggedPoint]) > handleSize * 2)
                        editedBezier[1] -= dragDelta;
                    if (editedBezierIndex > 0)
                    {
                        var prevBezier = bezierList.PreviousItem(editedBezierIndex);
                        prevBezier[3] = editedBezier[0];
                        if(prevBezier.type == Bezier.Type.Cubic && editedBezier.type == Bezier.Type.Cubic)
                            prevBezier[2] -= dragDelta;
                      
                    }
                }
                else if (draggedPoint == 1)
                {
                    if (editedBezierIndex > 0)
                    {
                        var prevBezier = bezierList.PreviousItem(editedBezierIndex);
                        if(editedBezier.smooth && editedBezier.type == Bezier.Type.Cubic && prevBezier.type == Bezier.Type.Cubic)
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
                    
                    if(editedBezier.type == Bezier.Type.Cubic && Vector3.Distance(editedBezier[2], editedBezier[draggedPoint]) > handleSize * 2)
                        editedBezier[2] -= dragDelta;
                    if (editedBezierIndex < bezierList.Count - 1)
                    {
                        var nextBezier = bezierList.NextItem(editedBezierIndex);
                        nextBezier[0] = editedBezier[3];
                        if (nextBezier.type == Bezier.Type.Cubic && editedBezier.type == Bezier.Type.Cubic)
                            nextBezier[1] -= dragDelta;
                    }
                }
                spline.OnSplineChange();
            }

            if (mouse0Down && !isMouseOverPoint)
            {
                draggedPoint = -1;

            }
            if (mouseUp)
            {
                spline.OnSplineChange();
                EditorUtility.SetDirty(target);
                dragging = false;
                draggedPoint = -1;
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
            var handleSize = HandleUtility.GetHandleSize(transform.position) * .05f;
            if(editedBezierIndex >= 0 && spline.beziers.Count > 0)
            {
                var editedBezier = spline.beziers[editedBezierIndex];
                var h0 = transform.TransformPoint(editedBezier[0]);
                var h3 = transform.TransformPoint(editedBezier[3]);
                Handles.color = Color.green;
                Handles.DrawSolidDisc(h0, Vector3.forward, handleSize * 2);
                Handles.DrawSolidDisc(h3, Vector3.forward, handleSize * 2);
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
                    Handles.DrawSolidDisc(h1, Vector3.forward, handleSize);
                    Handles.DrawSolidDisc(h2, Vector3.forward, handleSize);
                }
                else if(bezier.type == Bezier.Type.Quadratic)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawLine(h0, h1);
                    Handles.color = Color.gray.SetAlpha(.5f);
                    Handles.DrawSolidDisc(h1, Vector3.forward, handleSize);
                }


                var world0 = transform.TransformPoint(bezier[0]);
                var world1 = transform.TransformPoint(bezier[1]);
                var world2 = transform.TransformPoint(bezier[2]);
                var world3 = transform.TransformPoint(bezier[3]);
                Handles.color = Color.blue.SetAlpha(.5f);
                Handles.DrawSolidDisc(world0, Vector3.forward, handleSize);
                if (i == spline.beziers.Count - 1)
                {
                    Handles.DrawSolidDisc(world3, Vector3.forward, handleSize);
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

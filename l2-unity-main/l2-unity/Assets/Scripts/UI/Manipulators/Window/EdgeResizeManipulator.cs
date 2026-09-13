using UnityEngine;
using UnityEngine.UIElements;

// Redimensionnement par une bordure : la bande droite change la largeur, la
// bande du haut la hauteur. La fenetre de chat est ancree en bas a gauche,
// donc tirer vers le haut ou vers la droite agrandit.
public class EdgeResizeManipulator : PointerManipulator
{
    public enum Edge { Right, Top, Bottom }

    private readonly VisualElement _root;
    private readonly Edge _edge;
    private readonly float _min;
    private readonly float _max;
    private readonly float _snapSize;
    private readonly float _snapOffset;

    private Vector2 _startMousePosition;
    private float _originalSize;

    public EdgeResizeManipulator(VisualElement target, VisualElement root, Edge edge, float min, float max, float snapSize, float snapOffset)
    {
        this.target = target;
        _root = root;
        _edge = edge;
        _min = min;
        _max = max;
        _snapSize = snapSize;
        _snapOffset = snapOffset;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        _startMousePosition = evt.position;
        _originalSize = Mathf.Clamp(_edge == Edge.Right ? _root.resolvedStyle.width : _root.resolvedStyle.height, _min, _max);

        target.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!target.HasPointerCapture(evt.pointerId))
        {
            return;
        }

        if (_edge == Edge.Right)
        {
            float width = _originalSize + (evt.position.x - _startMousePosition.x);
            _root.style.width = Mathf.Clamp(width, _min, _max);
        }
        else if (_edge == Edge.Bottom)
        {
            float height = _originalSize + (evt.position.y - _startMousePosition.y);
            _root.style.height = Mathf.Clamp(height, _min, _max);
        }
        else
        {
            float height = Mathf.Clamp(_originalSize - (evt.position.y - _startMousePosition.y), _min, _max);
            if (_snapSize > 0f)
            {
                height = height - height % _snapSize + _snapOffset;
            }
            _root.style.height = height;
        }

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (target.HasPointerCapture(evt.pointerId))
        {
            target.ReleasePointer(evt.pointerId);
        }

        evt.StopPropagation();
    }
}

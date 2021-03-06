﻿using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*
 * TODO @hiko 
 * get the acutal delta from drag
 * should make it abstract later
 */

namespace HikoShit.UI
{
    [SelectionBase]
    //[ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class BaseScrollRect : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler, ICanvasElement, ILayoutElement//, ILayoutGroup
    {
        #region enum define

        public enum MovementType
        {
            Unrestricted, // Unrestricted movement -- can scroll forever
            Elastic, // Restricted but flexible -- can go past the edges, but springs back in place
            Clamped, // Restricted movement where it's not possible to go past the edges
        }

        public enum ScrollbarVisibility
        {
            Permanent,
            AutoHide,
            AutoHideAndExpandViewport,
        }

        #endregion

        [Serializable]
        public class ScrollRectEvent : UnityEvent<Vector2> { }

        [SerializeField]
        private RectTransform m_DragContent;
        public RectTransform dragContent { get { return m_DragContent; } set { m_DragContent = value; } }

        [SerializeField]
        private bool m_Horizontal = true;
        public bool horizontal { get { return m_Horizontal; } set { m_Horizontal = value; } }

        [SerializeField]
        private bool m_Vertical = true;
        public bool vertical { get { return m_Vertical; } set { m_Vertical = value; } }

        [SerializeField]
        private MovementType m_MovementType = MovementType.Elastic;
        public MovementType movementType { get { return m_MovementType; } set { m_MovementType = value; } }

        [SerializeField]
        private float m_Elasticity = 0.1f; // Only used for MovementType.Elastic
        public float elasticity { get { return m_Elasticity; } set { m_Elasticity = value; } }

        [SerializeField]
        private bool m_Inertia = true;
        public bool inertia { get { return m_Inertia; } set { m_Inertia = value; } }

        [SerializeField]
        private float m_DecelerationRate = 0.135f; // Only used when inertia is enabled
        public float decelerationRate { get { return m_DecelerationRate; } set { m_DecelerationRate = value; } }

        [SerializeField]
        private RectTransform m_Viewport;
        public RectTransform viewport { get { return m_Viewport; } set { m_Viewport = value; SetDirtyCaching(); } }

        [SerializeField]
        private ScrollRectEvent m_OnValueChanged = new ScrollRectEvent();
        public ScrollRectEvent onValueChanged { get { return m_OnValueChanged; } set { m_OnValueChanged = value; } }

        // The offset from handle position to mouse down position
        private Vector2 m_PointerStartLocalCursor = Vector2.zero;
        protected Vector2 m_ContentStartPosition = Vector2.zero;

        private RectTransform m_ViewRect;
        protected RectTransform viewRect
        {
            get
            {
                if (m_ViewRect == null)
                    m_ViewRect = m_Viewport;
                if (m_ViewRect == null)
                    m_ViewRect = (RectTransform)transform;
                return m_ViewRect;
            }
        }

        protected Bounds m_ContentBounds;
        private Bounds m_ViewBounds;

        private Vector2 m_Velocity;
        public Vector2 velocity { get { return m_Velocity; } set { m_Velocity = value; } }

        private bool m_Dragging;

        private Vector2 m_PrevPosition;
        private Bounds m_PrevContentBounds;
        private Bounds m_PrevViewBounds;
        [NonSerialized]
        private bool m_HasRebuiltLayout = false;

        [System.NonSerialized] private RectTransform m_Rect;
        private RectTransform rectTransform
        {
            get
            {
                if (m_Rect == null)
                    m_Rect = GetComponent<RectTransform>();
                return m_Rect;
            }
        }

        //private DrivenRectTransformTracker m_Tracker;

        /// <summary>
        /// 
        /// </summary>
        public event System.Action<Vector2> OnContentPositionChanged;
        [Header("test stuff")]
        [SerializeField]
        private Vector2 m_contentCenterPos; // maybe use it for simulate content move?
        [SerializeField]
        private Vector2 m_contentSize;

        public virtual void Rebuild(CanvasUpdate executing)
        {
            if (executing == CanvasUpdate.Prelayout)
            {
                UpdateCachedData();
            }

            if (executing == CanvasUpdate.PostLayout)
            {
                UpdateBounds();
                UpdatePrevData();

                m_HasRebuiltLayout = true;
            }
        }

        protected BaseScrollRect() { }

        public virtual void LayoutComplete() { }

        public virtual void GraphicUpdateComplete() { }

        private void UpdateCachedData()
        {
            Transform transform = this.transform;

            // These are true if either the elements are children, or they don't exist at all.
            bool viewIsChild = (viewRect.parent == transform);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }

        protected override void OnDisable()
        {
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

            m_HasRebuiltLayout = false;
            //m_Tracker.Clear();
            m_Velocity = Vector2.zero;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            base.OnDisable();
        }

        public override bool IsActive()
        {
            return base.IsActive() && m_DragContent != null;
        }

        private void EnsureLayoutHasRebuilt()
        {
            if (!m_HasRebuiltLayout && !CanvasUpdateRegistry.IsRebuildingLayout())
                Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentCenterPosition"> the anchor pos when anchor is center </param>
        /// <param name="contentSize"></param>
        public void SetVirtualContent(Vector2 contentCenterPosition, Vector2 contentSize)
        {
            m_contentCenterPos = contentCenterPosition;
            m_contentSize = contentSize;
            m_ContentBounds = new Bounds(m_contentCenterPos, contentSize);
        }

        public virtual void OnScroll(PointerEventData data)
        {
            if (!IsActive())
                return;

            EnsureLayoutHasRebuilt();
            UpdateBounds();

            Vector2 delta = data.scrollDelta;
            // Down is positive for scroll events, while in UI system up is positive.
            delta.y *= -1;
            if (vertical && !horizontal)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    delta.y = delta.x;
                delta.x = 0;
            }
            if (horizontal && !vertical)
            {
                if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                    delta.x = delta.y;
                delta.y = 0;
            }

            // TODO @Hiko should also do stuff for elastic move
            //Debug.Log($"get on scroll data {delta}");
            //m_OnValueChangedDelta?.Invoke(delta * 0.1f * m_ViewRect.rect.height * 0.1f);

            //m_OnValueChangedDelta?.Invoke(delta * m_ViewRect.rect.height * 0.1f);

            //Vector2 position = m_DragContent.anchoredPosition;
            //position += delta * m_ScrollSensitivity;
            //if (m_MovementType == MovementType.Clamped)
            //position += CalculateOffset(position - m_DragContent.anchoredPosition);

            //SetContentAnchoredPosition(position);
            UpdateBounds();
        }

        public virtual void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            m_Velocity = Vector2.zero;
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (!IsActive())
                return;

            UpdateBounds();

            m_PointerStartLocalCursor = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out m_PointerStartLocalCursor);
            m_ContentStartPosition = m_contentCenterPos;
            m_Dragging = true;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            m_Dragging = false;
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (!IsActive())
                return;

            Vector2 localCursor = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, eventData.position, eventData.pressEventCamera, out localCursor))
                return;

            UpdateBounds();

            Vector2 pointerDelta = localCursor - m_PointerStartLocalCursor;
            //Debug.Log($"check pointer delta {pointerDelta}");

            Vector2 nextPosition = CalculateNextPosition(pointerDelta);
            ApplyContentCenterPosition(nextPosition);
        }

        private Vector2 CalculateNextPosition(Vector2 pointerDelta)
        {
            Vector2 nextPostion = m_ContentStartPosition + pointerDelta;
            Vector2 offset = CalculateOffset(nextPostion - m_contentCenterPos);
            nextPostion += offset;

            if (m_MovementType == MovementType.Elastic)
            {
                if (offset.x != 0)
                    nextPostion.x = nextPostion.x - RubberDelta(offset.x, m_ViewBounds.size.x);
                if (offset.y != 0)
                    nextPostion.y = nextPostion.y - RubberDelta(offset.y, m_ViewBounds.size.y);
            }

            return nextPostion;
        }

        private void ApplyContentCenterPosition(Vector2 nextPosition)
        {
            if (!m_Horizontal)
                nextPosition.x = m_contentCenterPos.x;
            if (!m_Vertical)
                nextPosition.x = m_contentCenterPos.y;

            if (nextPosition != m_contentCenterPos)
            {
                m_contentCenterPos = nextPosition;
                UpdateBounds();
            }
            OnContentPositionChanged?.Invoke(m_contentCenterPos);
        }

        protected virtual void LateUpdate()
        {
            if (!m_DragContent)
                return;

            EnsureLayoutHasRebuilt();
            UpdateBounds();
            float deltaTime = Time.unscaledDeltaTime;

            Vector2 offset = CalculateOffset(Vector2.zero);
            if (!m_Dragging && (offset != Vector2.zero || m_Velocity != Vector2.zero))
            {
                Vector2 nextPosition = m_contentCenterPos;
                for (int axis = 0; axis < 2; axis++)
                {
                    // Apply spring physics if movement is elastic and content has an offset from the view.
                    if (m_MovementType == MovementType.Elastic && offset[axis] != 0)
                    {
                        float speed = m_Velocity[axis];
                        nextPosition[axis] = Mathf.SmoothDamp(m_contentCenterPos[axis], m_contentCenterPos[axis] + offset[axis], ref speed, m_Elasticity, Mathf.Infinity, deltaTime);
                        if (Mathf.Abs(speed) < 1) // remove it
                            speed = 0;
                        m_Velocity[axis] = speed;
                    }
                    // Else move content according to velocity with deceleration applied.
                    else if (m_Inertia)
                    {
                        m_Velocity[axis] *= Mathf.Pow(m_DecelerationRate, deltaTime);
                        if (Mathf.Abs(m_Velocity[axis]) < 1)
                            m_Velocity[axis] = 0;
                        nextPosition[axis] += m_Velocity[axis] * deltaTime;
                    }
                    // If we have neither elaticity or friction, there shouldn't be any velocity.
                    else
                    {
                        m_Velocity[axis] = 0;
                    }
                }

                if (m_Velocity != Vector2.zero)
                {
                    if (m_MovementType == MovementType.Clamped)
                    {
                        offset = CalculateOffset(nextPosition - m_DragContent.anchoredPosition);
                        nextPosition += offset;
                    }

                    ApplyContentCenterPosition(nextPosition);
                }
            }

            if (m_Dragging && m_Inertia)
            {
                Vector3 newVelocity = (m_contentCenterPos - m_PrevPosition) / deltaTime;
                m_Velocity = Vector3.Lerp(m_Velocity, newVelocity, deltaTime * 10);
            }

            if (m_ViewBounds != m_PrevViewBounds || m_ContentBounds != m_PrevContentBounds || m_contentCenterPos != m_PrevPosition)
            {
                //UISystemProfilerApi.AddMarker("ScrollRect.value", this);
                m_OnValueChanged.Invoke(normalizedPosition);
                UpdatePrevData();
            }
        }

        protected void UpdatePrevData()
        {
            if (m_DragContent == null)
                m_PrevPosition = Vector2.zero;
            else
                m_PrevPosition = m_contentCenterPos;

            m_PrevViewBounds = m_ViewBounds;
            m_PrevContentBounds = m_ContentBounds;
        }

        public Vector2 normalizedPosition
        {
            get
            {
                return new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition);
            }
            set
            {
                SetNormalizedPosition(value.x, 0);
                SetNormalizedPosition(value.y, 1);
            }
        }

        public float horizontalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                if (m_ContentBounds.size.x <= m_ViewBounds.size.x)
                    return (m_ViewBounds.min.x > m_ContentBounds.min.x) ? 1 : 0;
                return (m_ViewBounds.min.x - m_ContentBounds.min.x) / (m_ContentBounds.size.x - m_ViewBounds.size.x);
            }
            set
            {
                SetNormalizedPosition(value, 0);
            }
        }

        public float verticalNormalizedPosition
        {
            get
            {
                UpdateBounds();
                if (m_ContentBounds.size.y <= m_ViewBounds.size.y)
                    return (m_ViewBounds.min.y > m_ContentBounds.min.y) ? 1 : 0;
                ;
                return (m_ViewBounds.min.y - m_ContentBounds.min.y) / (m_ContentBounds.size.y - m_ViewBounds.size.y);
            }
            set
            {
                SetNormalizedPosition(value, 1);
            }
        }

        protected virtual void SetNormalizedPosition(float value, int axis)
        {
            // TODO @Hiko set scroll by real content height

            EnsureLayoutHasRebuilt();
            UpdateBounds();
            // How much the content is larger than the view.
            float hiddenLength = m_ContentBounds.size[axis] - m_ViewBounds.size[axis];
            // Where the position of the lower left corner of the content bounds should be, in the space of the view.
            float contentBoundsMinPosition = m_ViewBounds.min[axis] - value * hiddenLength;
            // The new content localPosition, in the space of the view.
            float newLocalPosition = m_DragContent.localPosition[axis] + contentBoundsMinPosition - m_ContentBounds.min[axis];

            // change this cuz we dun use content for this

            // Vector3 localPosition = m_Content.localPosition;
            // if (Mathf.Abs(localPosition[axis] - newLocalPosition) > 0.01f)
            // {
            //     localPosition[axis] = newLocalPosition;
            //     m_Content.localPosition = localPosition;
            //     m_Velocity[axis] = 0;
            //     UpdateBounds();
            // }
        }

        private static float RubberDelta(float overStretching, float viewSize)
        {
            return (1 - (1 / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1))) * viewSize * Mathf.Sign(overStretching);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            SetDirty();
        }

        #region layout stuff

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        public virtual float minWidth { get { return -1; } }
        public virtual float preferredWidth { get { return -1; } }
        public virtual float flexibleWidth { get { return -1; } }

        public virtual float minHeight { get { return -1; } }
        public virtual float preferredHeight { get { return -1; } }
        public virtual float flexibleHeight { get { return -1; } }

        public virtual int layoutPriority { get { return -1; } }

        //public virtual void SetLayoutHorizontal()
        //{
        //    m_Tracker.Clear();

        //    //if (m_HSliderExpand || m_VSliderExpand)
        //    //{
        //    //    m_Tracker.Add(this, viewRect,
        //    //        DrivenTransformProperties.Anchors |
        //    //        DrivenTransformProperties.SizeDelta |
        //    //        DrivenTransformProperties.AnchoredPosition);

        //    //    // Make view full size to see if content fits.
        //    //    viewRect.anchorMin = Vector2.zero;
        //    //    viewRect.anchorMax = Vector2.one;
        //    //    viewRect.sizeDelta = Vector2.zero;
        //    //    viewRect.anchoredPosition = Vector2.zero;

        //    //    // Recalculate content layout with this size to see if it fits when there are no scrollbars.
        //    //    LayoutRebuilder.ForceRebuildLayoutImmediate(dragContent);
        //    //    m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
        //    //    m_ContentBounds = GetBounds();

        //    //// If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it.
        //    //if (m_VSliderExpand && vScrollingNeeded)
        //    //{
        //    //    viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);

        //    //    // Recalculate content layout with this size to see if it fits vertically
        //    //    // when there is a vertical scrollbar (which may reflowed the content to make it taller).
        //    //    LayoutRebuilder.ForceRebuildLayoutImmediate(dragContent);
        //    //    m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
        //    //    m_ContentBounds = GetBounds();
        //    //}

        //    //// If it doesn't fit horizontally, enable horizontal scrollbar and shrink view vertically to make room for it.
        //    //if (m_HSliderExpand && hScrollingNeeded)
        //    //{
        //    //    viewRect.sizeDelta = new Vector2(viewRect.sizeDelta.x, -(m_HSliderHeight + m_HorizontalScrollbarSpacing));
        //    //    m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
        //    //    m_ContentBounds = GetBounds();
        //    //}

        //    //// If the vertical slider didn't kick in the first time, and the horizontal one did,
        //    //// we need to check again if the vertical slider now needs to kick in.
        //    //// If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it.
        //    //if (m_VSliderExpand && vScrollingNeeded && viewRect.sizeDelta.x == 0 && viewRect.sizeDelta.y < 0)
        //    //{
        //    //    viewRect.sizeDelta = new Vector2(-(m_VSliderWidth + m_VerticalScrollbarSpacing), viewRect.sizeDelta.y);
        //    //}
        //}

        //public virtual void SetLayoutVertical()
        //{
        //    m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
        //    m_ContentBounds = GetBounds();
        //}

        #endregion

        // @Hiko to understand this
        protected void UpdateBounds()
        {
            m_ViewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);
            m_ContentBounds = new Bounds(m_contentCenterPos, m_contentSize);

            if (m_DragContent == null)
                return;

            Vector3 contentSize = m_ContentBounds.size;
            Vector3 contentPos = m_ContentBounds.center;
            Vector2 contentPivot = m_contentCenterPos;
            AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
            m_ContentBounds.size = contentSize;
            m_ContentBounds.center = contentPos;

            if (movementType == MovementType.Clamped)
            {
                // Adjust content so that content bounds bottom (right side) is never higher (to the left) than the view bounds bottom (right side).
                // top (left side) is never lower (to the right) than the view bounds top (left side).
                // All this can happen if content has shrunk.
                // This works because content size is at least as big as view size (because of the call to InternalUpdateBounds above).
                Vector2 delta = Vector2.zero;
                if (m_ViewBounds.max.x > m_ContentBounds.max.x)
                {
                    delta.x = Math.Min(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
                }
                else if (m_ViewBounds.min.x < m_ContentBounds.min.x)
                {
                    delta.x = Math.Max(m_ViewBounds.min.x - m_ContentBounds.min.x, m_ViewBounds.max.x - m_ContentBounds.max.x);
                }

                if (m_ViewBounds.min.y < m_ContentBounds.min.y)
                {
                    delta.y = Math.Max(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
                }
                else if (m_ViewBounds.max.y > m_ContentBounds.max.y)
                {
                    delta.y = Math.Min(m_ViewBounds.min.y - m_ContentBounds.min.y, m_ViewBounds.max.y - m_ContentBounds.max.y);
                }
                if (delta.sqrMagnitude > float.Epsilon)
                {
                    contentPos = m_contentCenterPos + delta;
                    if (!m_Horizontal)
                        contentPos.x = m_contentCenterPos.x;

                    if (!m_Vertical)
                        contentPos.y = m_contentCenterPos.y;

                    AdjustBounds(ref m_ViewBounds, ref contentPivot, ref contentSize, ref contentPos);
                }
            }
        }

        internal static void AdjustBounds(ref Bounds viewBounds, ref Vector2 contentPivot, ref Vector3 contentSize, ref Vector3 contentPos)
        {
            // Make sure content bounds are at least as large as view by adding padding if not.
            // One might think at first that if the content is smaller than the view, scrolling should be allowed.
            // However, that's not how scroll views normally work.
            // Scrolling is *only* possible when content is *larger* than view.
            // We use the pivot of the content rect to decide in which directions the content bounds should be expanded.
            // E.g. if pivot is at top, bounds are expanded downwards.
            // This also works nicely when ContentSizeFitter is used on the content.
            Vector3 excess = viewBounds.size - contentSize;
            if (excess.x > 0)
            {
                contentPos.x -= excess.x * (contentPivot.x - 0.5f);
                contentSize.x = viewBounds.size.x;
            }
            if (excess.y > 0)
            {
                contentPos.y -= excess.y * (contentPivot.y - 0.5f);
                contentSize.y = viewBounds.size.y;
            }
        }

        private readonly Vector3[] m_Corners = new Vector3[4];
        private Bounds GetBounds(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return new Bounds();
            rectTransform.GetWorldCorners(m_Corners);
            var viewWorldToLocalMatrix = viewRect.worldToLocalMatrix;
            return InternalGetBounds(m_Corners, ref viewWorldToLocalMatrix);
        }

        internal static Bounds InternalGetBounds(Vector3[] corners, ref Matrix4x4 viewWorldToLocalMatrix)
        {
            var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int j = 0; j < 4; j++)
            {
                Vector3 v = viewWorldToLocalMatrix.MultiplyPoint3x4(corners[j]);
                vMin = Vector3.Min(v, vMin);
                vMax = Vector3.Max(v, vMax);
            }

            var bounds = new Bounds(vMin, Vector3.zero);
            bounds.Encapsulate(vMax);
            return bounds;
        }

        private Vector2 CalculateOffset(Vector2 delta)
        {
            return InternalCalculateOffset(ref m_ViewBounds, ref m_ContentBounds, m_Horizontal, m_Vertical, m_MovementType, ref delta);
        }

        private Vector2 InternalCalculateOffset(ref Bounds viewBounds, ref Bounds contentBounds, bool horizontal, bool vertical, MovementType movementType, ref Vector2 delta)
        {
            Vector2 offset = Vector2.zero;
            if (movementType == MovementType.Unrestricted)
                return offset;

            Vector2 min = contentBounds.min;
            Vector2 max = contentBounds.max;

            if (horizontal)
            {
                min.x += delta.x;
                max.x += delta.x;
                if (min.x > viewBounds.min.x)
                    offset.x = viewBounds.min.x - min.x;
                else if (max.x < viewBounds.max.x)
                    offset.x = viewBounds.max.x - max.x;
            }

            if (vertical)
            {
                min.y += delta.y;
                max.y += delta.y;
                if (max.y < viewBounds.max.y)
                    offset.y = viewBounds.max.y - max.y;
                else if (min.y > viewBounds.min.y)
                    offset.y = viewBounds.min.y - min.y;
            }

            return offset;
        }

        protected void SetDirty()
        {
            if (!IsActive())
                return;

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected void SetDirtyCaching()
        {
            if (!IsActive())
                return;

            CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

#if UNITY_EDITOR

        protected override void OnValidate()
        {
            SetDirtyCaching();
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                DrawVirtualContent();
                DebugDrawer.DrawBounds(m_ViewBounds, Color.yellow);
                DebugDrawer.DrawBounds(m_ContentBounds, Color.red);
            }
        }

        private void DrawVirtualContent()
        {
            float width = m_contentSize.x;
            float height = m_contentSize.y;

            // to get top left point
            Vector3 position = m_ViewRect.position + (Vector3)m_contentCenterPos;

            Vector3 point1 = default, point2 = default;
            point1 = position;
            point1.x -= width * 0.5f;
            point1.y += height * 0.5f;
            point2 = point1;
            point2.x += width;

            Vector3 point3 = point1, point4 = point2;
            point3.y -= height;
            point4.y -= height;

            Debug.DrawLine(point1, point2, Color.blue);
            Debug.DrawLine(point1, point3, Color.blue);
            Debug.DrawLine(point2, point4, Color.blue);
            Debug.DrawLine(point3, point4, Color.blue);
        }

#endif
    }
}
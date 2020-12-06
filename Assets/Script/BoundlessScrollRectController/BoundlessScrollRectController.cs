﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
@Hiko cant support Unrestricted move for now :)
*/

[RequireComponent(typeof(ScrollRect), typeof(GridLayoutGroup))]
public class BoundlessScrollRectController : MonoBehaviour
{
    [SerializeField]
    private ScrollRect m_scrollRect = null;

    [SerializeField]
    private RectTransform m_viewport = null;
    [SerializeField, Tooltip("the content that used to drag")]
    private RectTransform m_dragContent = null;
    [SerializeField, Tooltip("another content hold UI elements")]
    private RectTransform m_actualContent = null;
    // these 2 content anchor are on top left 

    private int m_viewItemCount = 0;
    private int m_viewItemCountInRow = 0;
    private int m_viewItemCountInColumn = 0;

    // get rid of those viriable, use them from scrollrect
    private bool m_isVertical = true;
    private bool m_isHorizontal = false;
    private int m_startIndex = 0;
    private const int CACHE_COUNT = 2;

    private Vector2 m_itemSize = Vector2.zero * 100.0f;
    private Vector2 m_itemStartPos = default; // the showing first item start pos in virwport

    /// <summary>
    /// including spacing
    /// </summary>
    private Vector2 m_actualContentSize = default;

    private List<BoundlessBaseScrollRectItem> m_uiItems = null;
    private IReadOnlyList<IBoundlessScrollRectItemData> m_dataList = null;

    /* a test component, we will move this component
    * and use this to setup the grid size
    */
    [Space, Header("Grid Layout Setting"), SerializeField]
    private BoundlessGridLayoutData m_gridLayoutGroup = default;

#if UNITY_EDITOR
    [Space, Header("Debug settings")]
    public bool m_drawContentSize = true;
    public bool m_drawGrids = true;
    public bool m_drawShowingGrids = true;
#endif

    public void RefreshLayout()
    {
        // if value on inspector got changed or some value being changed by code, should also call this
        if (null == m_dataList)
            return;
        UpdateAcutalContentSize();
        OnScrollRectValueChanged(Vector2.zero);
    }

    public void InjectData(IReadOnlyList<IBoundlessScrollRectItemData> dataList)
    {
        m_dataList = dataList;
        m_startIndex = 0;

        // to set actual content correctly?
        m_actualContent.anchorMax = m_viewport.anchorMax;
        m_actualContent.anchorMin = m_viewport.anchorMin;
        m_actualContent.pivot = m_viewport.pivot;

        m_actualContent.localPosition = m_viewport.localPosition;
        m_actualContent.anchoredPosition = m_viewport.anchoredPosition;
        m_actualContent.sizeDelta = m_viewport.sizeDelta;

        SyncSize();
        // set default simple draw stuff
        SpawnCachedItems();

        for (int i = 0; i < m_uiItems.Count; i++)
        {
            if (i < dataList.Count)
            {
                m_uiItems[i].InjectData(dataList[i]);
                //m_uiItems[i].Setup(dataList[i].TempName);
            }

            Vector3 pos = m_uiItems[i].ItemRectTransform.anchoredPosition3D;
            pos -= i * m_uiItems[i].ItemRectTransform.up * m_itemSize.y;
            m_uiItems[i].ItemRectTransform.anchoredPosition = pos;
            m_uiItems[i].gameObject.SetActive(true);
        }

        m_itemStartPos.y = 1.0f;
        UpdateAcutalContentSize();
        OnScrollRectValueChanged(Vector2.zero);
    }

    private void UpdateAcutalContentSize()
    {
        Vector2 result = default;
        Vector2 cellSize = m_itemSize;
        // to use it later
        // RectOffset padding = m_layoutData.padding;
        Vector2 spacing = m_gridLayoutGroup.spacing;
        int dataCount = m_dataList.Count;

        // TODO @Hiko when calaulate size, should also deal with padding
        int constraintCount = m_gridLayoutGroup.constraintCount;
        int dynamicCount = (dataCount % constraintCount > 0) ? (dataCount / constraintCount) + 1 : (dataCount / constraintCount);
        if (m_gridLayoutGroup.constraint == BoundlessGridLayoutData.Constraint.FixedColumnCount)
        {
            result.x = (constraintCount * m_itemSize.x) + ((constraintCount - 1) * spacing.x);
            result.y = dynamicCount * m_itemSize.y + (dynamicCount - 1) * spacing.y;
        }
        else if (m_gridLayoutGroup.constraint == BoundlessGridLayoutData.Constraint.FixedRowCount)
        {
            result.y = (constraintCount * m_itemSize.y) + ((constraintCount - 1) * spacing.y);
            result.x = dynamicCount * m_itemSize.x + (dynamicCount - 1) * spacing.x;
        }

        m_actualContentSize = result;
        m_dragContent.sizeDelta = m_actualContentSize;
    }

    private void OnScrollRectValueChanged(Vector2 position)
    {
        RefreshItemStartPosition();
        TestDrawContent();
    }

    private void RefreshItemStartPosition()
    {
        UpdateAcutalContentSize();

        float minStartPosX = 0.0f, maxStartPosX = 0.0f;
        float minStartPosY = 0.0f, maxStartPosY = 0.0f;
        if (BoundlessGridLayoutData.Constraint.FixedColumnCount == m_gridLayoutGroup.constraint)
        {
            // content may move vertical
            // start from left to right for test
            // start from up to down for test
            minStartPosY = 0.0f;
            maxStartPosY = m_actualContentSize.y - m_viewport.rect.height;

            minStartPosX = 0.0f;
            maxStartPosX = m_viewport.rect.width - m_actualContentSize.x;
        }
        else if (BoundlessGridLayoutData.Constraint.FixedRowCount == m_gridLayoutGroup.constraint)
        {
            // content may move horizontal or...
            // start from left to right for test
            // start from up to down for test
            minStartPosY = 0.0f;
            maxStartPosY = m_actualContentSize.y - m_viewport.rect.height;

            minStartPosX = 0.0f;
            maxStartPosX = m_viewport.rect.width - m_actualContentSize.x;
        }

        Vector2 nextTopPos = new Vector2(m_dragContent.anchoredPosition.x, m_dragContent.anchoredPosition.y);
        nextTopPos.x = Mathf.Clamp(nextTopPos.x, Mathf.Min(minStartPosX, maxStartPosX), Mathf.Max(minStartPosX, maxStartPosX));
        nextTopPos.y = Mathf.Clamp(nextTopPos.y, Mathf.Min(minStartPosY, maxStartPosY), Mathf.Max(minStartPosY, maxStartPosY));
        m_itemStartPos = nextTopPos;
    }

    private void TestDrawContent()
    {
        Vector3 ogPosition = m_viewport.position;
        Vector3 dragContentPostion = m_dragContent.position;
        Vector3 dragAnchorContentPostion = m_dragContent.anchoredPosition;
        m_actualContent.anchoredPosition = Vector2.zero;

        // to get position delta
        float xMove = Mathf.Abs(dragAnchorContentPostion.x);
        float yMove = Mathf.Abs(dragAnchorContentPostion.y);
        Vector2 itemSize = m_gridLayoutGroup.cellSize;
        Vector2 spacing = m_gridLayoutGroup.spacing;
        int tempColumnIndex = Mathf.FloorToInt(xMove / (itemSize.x + spacing.x));
        int tempRowIndex = Mathf.FloorToInt(yMove / (itemSize.y + spacing.y));

        // deal with different start axis
        Vector3 tempMove = new Vector3(tempColumnIndex * (itemSize.x + spacing.x), -tempRowIndex * (itemSize.y + spacing.y), 0.0f);
        Bounds contentBounds = new Bounds(m_dragContent.position + new Vector3(m_dragContent.rect.width * 0.5f, -m_dragContent.rect.height * 0.5f, 0.0f), m_dragContent.rect.size);

        // to calculate it somewhere else :)
        int rowDataCount = 0, columnDataCount = 0;
        if (BoundlessGridLayoutData.Constraint.FixedColumnCount == m_gridLayoutGroup.constraint)
        {
            rowDataCount = m_gridLayoutGroup.constraintCount;
            columnDataCount = (int)Mathf.CeilToInt((float)m_dataList.Count / rowDataCount);
        }
        else
        {
            columnDataCount = m_gridLayoutGroup.constraintCount;
            rowDataCount = (int)Mathf.CeilToInt((float)m_dataList.Count / columnDataCount);
        }

        // deal with content from left to right (simple case) first
        int rowFirstDataIndex = 0, dataIndex = 0;
        int uiItemIndex = 0;
        Vector3 rowTopLeftPosition = default, itemTopLeftPosition = default;
        rowTopLeftPosition = dragContentPostion + tempMove;
        Bounds gridBounds = new Bounds(rowTopLeftPosition, itemSize);
        Vector3 gridBoundsCenter = default;
        bool hideItem = false;

        // draw from left to right for test
        for (int rowIndex = 0; rowIndex < m_viewItemCountInColumn; rowIndex++)
        {
            itemTopLeftPosition = rowTopLeftPosition;
            if (BoundlessGridLayoutData.StartAxis.Horizontal == m_gridLayoutGroup.startAxis)
                rowFirstDataIndex = (tempRowIndex + rowIndex) * rowDataCount;
            else
                rowFirstDataIndex = tempRowIndex + rowIndex + tempColumnIndex * columnDataCount;

            for (int columnIndex = 0; columnIndex < m_viewItemCountInRow; columnIndex++)
            {
                gridBoundsCenter = itemTopLeftPosition;
                gridBoundsCenter.x += itemSize.x;
                gridBoundsCenter.y -= itemSize.y;
                gridBounds.center = gridBoundsCenter;

                if (BoundlessGridLayoutData.StartAxis.Horizontal == m_gridLayoutGroup.startAxis)
                    dataIndex = rowFirstDataIndex + tempColumnIndex + columnIndex;
                else
                    dataIndex = rowFirstDataIndex + columnIndex * columnDataCount;

                hideItem = !contentBounds.Intersects(gridBounds) || dataIndex >= m_dataList.Count || dataIndex < 0;
                if (hideItem)
                {
                    m_uiItems[uiItemIndex].ItemRectTransform.position = Vector2.zero;
                    m_uiItems[uiItemIndex].Hide();
                }
                else
                {
                    m_uiItems[uiItemIndex].ItemRectTransform.position = itemTopLeftPosition;
                    m_uiItems[uiItemIndex].InjectData(m_dataList[dataIndex]);
                    m_uiItems[uiItemIndex].Show();
                    uiItemIndex++;
                }

                itemTopLeftPosition.x += spacing.x + itemSize.x;
            }
            rowTopLeftPosition.y -= spacing.y + itemSize.y;
        }

        while (uiItemIndex < m_uiItems.Count)
        {
            m_uiItems[uiItemIndex].Hide();
            m_uiItems[uiItemIndex].ItemRectTransform.anchoredPosition = Vector2.zero;
            uiItemIndex++;
        }
    }

    private void CalculateViewportShowCount()
    {
        m_viewItemCountInRow = 0;
        m_viewItemCountInColumn = 0;
        m_itemSize = m_gridLayoutGroup.cellSize;

        Vector2 spacing = m_gridLayoutGroup.spacing;
        float viewportHeight = Mathf.Abs(m_viewport.rect.height * m_viewport.localScale.y);
        float viewportWidth = Mathf.Abs(m_viewport.rect.width * m_viewport.localScale.y);
        m_viewItemCountInColumn = Mathf.CeilToInt(viewportHeight / (m_itemSize.y + spacing.y));
        m_viewItemCountInRow = Mathf.CeilToInt(viewportWidth / (m_itemSize.x + spacing.x));

        if (viewportHeight % (m_itemSize.y + spacing.y) > 0)
            m_viewItemCountInColumn++;

        if (viewportWidth % (m_itemSize.x + spacing.x) > 0)
            m_viewItemCountInRow++;

        m_viewItemCount = m_viewItemCountInRow * m_viewItemCountInColumn;
    }

    private void SpawnCachedItems()
    {
        if (null == m_uiItems)
            m_uiItems = new List<BoundlessBaseScrollRectItem>();

        BoundlessBaseScrollRectItem tempItem = null;
        for (int i = 0; i < m_viewItemCount; i++)
        {
            tempItem = Instantiate(m_gridLayoutGroup.GridItemPrefab, m_actualContent);
            m_uiItems.Add(tempItem);
            tempItem.Hide();
        }
    }

    private void ClearCachedItems()
    {
        if (null != m_uiItems)
            m_uiItems.Clear();
        DestroyAllChildren(m_actualContent);
    }

    private void ClampVelocityToToStop()
    {
        float sqrLimit = m_gridLayoutGroup.StopMagSqrVel;
        sqrLimit *= sqrLimit;
        float velocitySqrMag = m_scrollRect.velocity.sqrMagnitude;
        // if (!Mathf.Approximately(0.0f, velocitySqrMag))
        //     Debug.Log($"test vel {m_scrollRect.velocity}, test sqr mag {velocitySqrMag}");
        if (velocitySqrMag < sqrLimit && !Mathf.Approximately(0.0f, velocitySqrMag)) // try to clamped move to save 
            m_scrollRect.StopMovement();
    }

    #region mono method

    private void Reset()
    {
        m_scrollRect.GetComponent<ScrollRect>();
        m_isVertical = m_scrollRect.vertical;
        m_isHorizontal = m_scrollRect.horizontal;
        m_scrollRect.StopMovement();
        m_dragContent = m_scrollRect.content;
    }

    private void OnEnable()
    {
        m_scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        CalculateViewportShowCount();
        ClearCachedItems();
        SpawnCachedItems();
    }

    private void OnDisable()
    {
        m_scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
        ClearCachedItems();
    }

    private void Update()
    {
        ClampVelocityToToStop();
    }

    #endregion

    private void DestroyAllChildren(Transform target)
    {
        if (null == target)
            return;

        int childCount = target.childCount;
        for (int i = childCount - 1; i >= 0; i--)
            Destroy(target.GetChild(i).gameObject);
    }

    private void SyncSize()
    {
        // sync the size form grid component to actual content size
        m_itemSize = m_gridLayoutGroup.cellSize;
        if (null != m_uiItems)
            for (int i = 0; i < m_uiItems.Count; i++)
                m_uiItems[i].SetItemSize(m_itemSize);
    }

#if UNITY_EDITOR // some method to debug drawing or sth

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        if (m_drawContentSize)
            DrawDebugContentSize();

        if (m_drawGrids)
            DrawDebugGrids();

        if (m_drawShowingGrids)
            DrawDebugShowingGrids();
    }

    private void DrawDebugContentSize()
    {
        if (null == m_dataList)
            return;

        Vector2 actualContentSize = m_actualContentSize;

        Vector3 topLeftPoint = m_dragContent.position;
        Vector3 topRightPoint = topLeftPoint;
        topRightPoint.x += actualContentSize.x;

        Vector3 bottomLeftPoint = topLeftPoint;
        Vector3 BottomRightPoint = topRightPoint;
        bottomLeftPoint.y -= actualContentSize.y;
        BottomRightPoint.y -= actualContentSize.y;

        Debug.DrawLine(topLeftPoint, topRightPoint, Color.magenta);
        Debug.DrawLine(topLeftPoint, bottomLeftPoint, Color.magenta);
        Debug.DrawLine(topRightPoint, BottomRightPoint, Color.magenta);
        Debug.DrawLine(bottomLeftPoint, BottomRightPoint, Color.magenta);
    }

    /// <summary>
    /// let it draw full stuff for now
    /// @TODO need think about spacing
    /// </summary>
    private void DrawDebugGrids()
    {
        if (null == m_dataList)
            return;

        int dataCount = m_dataList.Count;
        Vector3 columnStartItemTopLeftPos = m_dragContent.position;
        Vector3 rowItemTopLeftPos = m_dragContent.position;
        Vector2 spacing = m_gridLayoutGroup.spacing;

        // should know which axis get constrained
        int constraintCount = m_gridLayoutGroup.constraintCount;
        int dynamicCount = (dataCount % constraintCount > 0) ? (dataCount / constraintCount) + 1 : (dataCount / constraintCount);
        if (BoundlessGridLayoutData.Constraint.FixedColumnCount == m_gridLayoutGroup.constraint)
        {
            for (int i = 0; i < dynamicCount; i++)
            {
                rowItemTopLeftPos = columnStartItemTopLeftPos;
                for (int j = 0; j < constraintCount; j++)
                {
                    DrawOneDebugGridItem(rowItemTopLeftPos, Color.blue);
                    rowItemTopLeftPos.x += spacing.x + m_itemSize.x;
                }
                columnStartItemTopLeftPos.y -= m_itemSize.y + spacing.y;
            }
        }
        else // if (BoundlessGridLayoutData.Constraint.FixedRowCount == m_gridLayoutGroup.constraint)
        {
            for (int i = 0; i < constraintCount; i++)
            {
                rowItemTopLeftPos = columnStartItemTopLeftPos;
                for (int j = 0; j < dynamicCount; j++)
                {
                    DrawOneDebugGridItem(rowItemTopLeftPos, Color.blue);
                    rowItemTopLeftPos.x += spacing.x + m_itemSize.x;
                }
                columnStartItemTopLeftPos.y -= m_itemSize.y + spacing.y;
            }
        }
    }

    private void DrawDebugShowingGrids()
    {
        Vector3 dragContentPostion = m_dragContent.position;
        Vector3 dragAnchorContentPostion = m_dragContent.anchoredPosition;

        // to get position delta
        float xMove = Mathf.Abs(dragAnchorContentPostion.x);
        float yMove = Mathf.Abs(dragAnchorContentPostion.y);

        Vector2 itemSize = m_gridLayoutGroup.cellSize;
        Vector2 spacing = m_gridLayoutGroup.spacing;
        int tempXIndex = Mathf.FloorToInt(xMove / (itemSize.x + spacing.x));
        int tempYIndex = Mathf.FloorToInt(yMove / (itemSize.y + spacing.y));
        Vector3 tempMove = new Vector3(tempXIndex * (itemSize.x + spacing.x), -tempYIndex * (itemSize.y + spacing.y), 0.0f);
        Bounds contentBounds = new Bounds(m_dragContent.position + new Vector3(m_dragContent.rect.width * 0.5f, -m_dragContent.rect.height * 0.5f, 0.0f), m_dragContent.rect.size);

        // deal with content from left to right (simple case) first
        Vector3 rowTopLeftPosition = default, itemTopLeftPosition = default;
        rowTopLeftPosition = dragContentPostion + tempMove;
        Bounds gridBounds = new Bounds(rowTopLeftPosition, itemSize);
        Vector3 gridBoundsCenter = default;
        for (int rowIndex = 0; rowIndex < m_viewItemCountInColumn; rowIndex++)
        {
            itemTopLeftPosition = rowTopLeftPosition;
            for (int columnIndex = 0; columnIndex < m_viewItemCountInRow; columnIndex++)
            {
                gridBoundsCenter = itemTopLeftPosition;
                gridBoundsCenter.x += itemSize.x;
                gridBoundsCenter.y -= itemSize.y;
                gridBounds.center = gridBoundsCenter;

                if (contentBounds.Intersects(gridBounds))
                    DrawOneDebugGridItem(itemTopLeftPosition, Color.white); // the real grid in the content
                else
                    DrawOneDebugGridItem(itemTopLeftPosition, Color.yellow); // the grid should not show

                itemTopLeftPosition.x += spacing.x + itemSize.x;
            }
            rowTopLeftPosition.y -= spacing.y + itemSize.y;
        }
    }

    private void DrawOneDebugGridItem(Vector3 topLeftPoint, Color color)
    {
        Vector3 itemSize = m_gridLayoutGroup.cellSize;

        Vector3 topRightPoint = topLeftPoint;
        topRightPoint.x += itemSize.x;

        Vector3 bottomLeftPoint = topLeftPoint;
        bottomLeftPoint.y -= itemSize.y;

        Vector3 bottomRightPoint = topRightPoint;
        bottomRightPoint.y -= itemSize.y;

        Debug.DrawLine(topLeftPoint, topRightPoint, color);
        Debug.DrawLine(bottomLeftPoint, bottomRightPoint, color);

        Debug.DrawLine(topLeftPoint, bottomLeftPoint, color);
        Debug.DrawLine(topRightPoint, bottomRightPoint, color);

        Debug.DrawLine(topLeftPoint, bottomRightPoint, color);
        Debug.DrawLine(topRightPoint, bottomLeftPoint, color);
    }

#endif

}

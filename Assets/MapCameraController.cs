using UnityEngine;

public class MapCameraController : MonoBehaviour
{
    //[Header("移动设置")]
    //public float panSpeed = 4f;      // 鼠标拖拽速度
    //public Vector2 panLimitX = new Vector2(0, 32768);
    //public Vector2 panLimitZ = new Vector2(0, 32768);
    //public float dragDamping = 5f;     // 惯性阻尼

    //[Header("缩放设置")]
    //public float scrollSpeed = 1f;    // 缩放速度
    //public float scrollSmoothTime = 0.1f;
    //public float minHeight = 0f;
    //public float maxHeight = 10000f;

    //private Camera cam;

    //// 拖拽
    //private bool isDragging = false;
    //private Vector3 lastMousePos;
    //private Vector3 dragVelocity;

    //// 滚轮缩放
    //private float targetHeight;
    //private float heightVelocity;

    //void Start()
    //{
    //    cam = GetComponent<Camera>();
    //    targetHeight = cam.transform.position.y;
    //}

    //void Update()
    //{
    //    HandleDrag();
    //    HandleScroll();
    //    ApplyMovement();
    //}

    //void HandleDrag()
    //{
    //    if (Input.GetMouseButtonDown(0))
    //    {
    //        isDragging = true;
    //        lastMousePos = Input.mousePosition;
    //        dragVelocity = Vector3.zero;
    //    }

    //    if (Input.GetMouseButtonUp(0))
    //    {
    //        isDragging = false;
    //    }

    //    if (isDragging)
    //    {
    //        Vector3 delta = Input.mousePosition - lastMousePos;
    //        // 转换成世界坐标移动，XZ 平面
    //        Vector3 move = new Vector3(-delta.x, 0, -delta.y) * panSpeed * (cam.transform.position.y / 50f);
    //        dragVelocity = Vector3.Lerp(dragVelocity, move, Time.deltaTime * dragDamping);
    //        lastMousePos = Input.mousePosition;
    //    }
    //}

    //void HandleScroll()
    //{
    //    float scroll = Input.GetAxis("Mouse ScrollWheel");
    //    if (scroll != 0f)
    //    {
    //        targetHeight -= scroll * scrollSpeed * 50f;
    //        targetHeight = Mathf.Clamp(targetHeight, minHeight, maxHeight);
    //    }
    //}

    //void ApplyMovement()
    //{
    //    // 惯性拖拽
    //    if (!isDragging)
    //    {
    //        dragVelocity = Vector3.Lerp(dragVelocity, Vector3.zero, Time.deltaTime * dragDamping);
    //    }

    //    Vector3 pos = cam.transform.position;
    //    pos += dragVelocity * Time.deltaTime;

    //    // 平滑滚轮缩放
    //    pos.y = Mathf.SmoothDamp(pos.y, targetHeight, ref heightVelocity, scrollSmoothTime);

    //    // 限制范围
    //    pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
    //    pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

    //    cam.transform.position = pos;
    //}





    [Header("移动设置")]
    public float panSpeed = 0.1f;      // 移动灵敏度
    public Vector2 panLimitX = new Vector2(0, 40075.017f);
    public Vector2 panLimitZ = new Vector2(0, 40075.017f);

    [Header("缩放设置")]
    public float scrollSpeed = 100f;    // 缩放速度
    public float minHeight = 5f;
    public float maxHeight = 10000f;

    private Camera cam;
    private Vector3 dragStartPosition;
    private Vector3 cameraStartPosition;
    private bool isDragging = false;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        HandleDrag();
        HandleScroll();
    }

    void HandleDrag()
    {
        // 开始拖拽
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPosition = Input.mousePosition;
            cameraStartPosition = cam.transform.position;
        }

        // 结束拖拽
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        // 拖拽中
        if (isDragging)
        {
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 mouseDelta = currentMousePos - dragStartPosition;

            // 根据相机高度调整移动速度（越高移动越快）
            float heightFactor = cam.transform.position.y / 100f;

            // 直接计算移动量，没有阻尼
            Vector3 move = new Vector3(-mouseDelta.x, 0, -mouseDelta.y) * panSpeed * heightFactor;
            Vector3 newPosition = cameraStartPosition + move;

            // 应用限制
            newPosition.x = Mathf.Clamp(newPosition.x, panLimitX.x, panLimitX.y);
            newPosition.z = Mathf.Clamp(newPosition.z, panLimitZ.x, panLimitZ.y);

            cam.transform.position = newPosition;
        }
    }

    void HandleScroll()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            Vector3 pos = cam.transform.position;

            // 直接修改高度，没有平滑过渡
            pos.y -= scroll * scrollSpeed;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);

            cam.transform.position = pos;
        }
    }

    // 可选：添加键盘移动支持
    void HandleKeyboard()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        if (horizontal != 0f || vertical != 0f)
        {
            Vector3 move = new Vector3(horizontal, 0, vertical) * panSpeed * Time.deltaTime * cam.transform.position.y;
            Vector3 newPosition = cam.transform.position + move;

            newPosition.x = Mathf.Clamp(newPosition.x, panLimitX.x, panLimitX.y);
            newPosition.z = Mathf.Clamp(newPosition.z, panLimitZ.x, panLimitZ.y);

            cam.transform.position = newPosition;
        }
    }
}

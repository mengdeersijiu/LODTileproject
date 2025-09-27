using UnityEngine;

public class MapCameraController : MonoBehaviour
{
    //[Header("�ƶ�����")]
    //public float panSpeed = 4f;      // �����ק�ٶ�
    //public Vector2 panLimitX = new Vector2(0, 32768);
    //public Vector2 panLimitZ = new Vector2(0, 32768);
    //public float dragDamping = 5f;     // ��������

    //[Header("��������")]
    //public float scrollSpeed = 1f;    // �����ٶ�
    //public float scrollSmoothTime = 0.1f;
    //public float minHeight = 0f;
    //public float maxHeight = 10000f;

    //private Camera cam;

    //// ��ק
    //private bool isDragging = false;
    //private Vector3 lastMousePos;
    //private Vector3 dragVelocity;

    //// ��������
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
    //        // ת�������������ƶ���XZ ƽ��
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
    //    // ������ק
    //    if (!isDragging)
    //    {
    //        dragVelocity = Vector3.Lerp(dragVelocity, Vector3.zero, Time.deltaTime * dragDamping);
    //    }

    //    Vector3 pos = cam.transform.position;
    //    pos += dragVelocity * Time.deltaTime;

    //    // ƽ����������
    //    pos.y = Mathf.SmoothDamp(pos.y, targetHeight, ref heightVelocity, scrollSmoothTime);

    //    // ���Ʒ�Χ
    //    pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
    //    pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

    //    cam.transform.position = pos;
    //}





    [Header("�ƶ�����")]
    public float panSpeed = 0.1f;      // �ƶ�������
    public Vector2 panLimitX = new Vector2(0, 40075.017f);
    public Vector2 panLimitZ = new Vector2(0, 40075.017f);

    [Header("��������")]
    public float scrollSpeed = 100f;    // �����ٶ�
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
        // ��ʼ��ק
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPosition = Input.mousePosition;
            cameraStartPosition = cam.transform.position;
        }

        // ������ק
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        // ��ק��
        if (isDragging)
        {
            Vector3 currentMousePos = Input.mousePosition;
            Vector3 mouseDelta = currentMousePos - dragStartPosition;

            // ��������߶ȵ����ƶ��ٶȣ�Խ���ƶ�Խ�죩
            float heightFactor = cam.transform.position.y / 100f;

            // ֱ�Ӽ����ƶ�����û������
            Vector3 move = new Vector3(-mouseDelta.x, 0, -mouseDelta.y) * panSpeed * heightFactor;
            Vector3 newPosition = cameraStartPosition + move;

            // Ӧ������
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

            // ֱ���޸ĸ߶ȣ�û��ƽ������
            pos.y -= scroll * scrollSpeed;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);

            cam.transform.position = pos;
        }
    }

    // ��ѡ����Ӽ����ƶ�֧��
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

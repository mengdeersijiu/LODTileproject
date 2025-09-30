using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class Gizmos_Terrain : MonoBehaviour
{
    Node root;
    public float rootSize = 40075.017f;
    public Vector3 terrainOffset;

    // 调试信息
    public int totalNodes = 0;
    public int leafNodes = 0;
    public bool showGizmos = true;
    public bool showLabels = true;

    [Header("LOD 参数")]
    [SerializeField, Range(0.01f, 10f)] float splitRatio = 2.0f;   // 比例小于此值就细分 2.0
    [SerializeField, Range(0.01f, 10f)] float mergeRatio = 3.0f;   // 比例大于此值就合并（要大于 splitRatio）
    [SerializeField, Range(0f, 1f)] float heightWeight = 0.2f; // Y 高度对距离的权重 0.2
    [SerializeField] int maxZ = 15;     //最大的层级
    [SerializeField] float magicNum = 2.0f; //调节lod细分的

    private static Material sharedMat;
    private static Dictionary<Node, MaterialPropertyBlock> propertyBlocks = new Dictionary<Node, MaterialPropertyBlock>();


    // 添加纹理缓存字典
    private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static Dictionary<string, int> textureReferenceCount = new Dictionary<string, int>();
    private int cleanupCounter = 0;

    // 获取纹理的唯一键
    private string GetTextureKey(int z, int x, int y)
    {
        return $"Tile_{z}_{x}_{y}";
    }

    void Start()
    {
        //DebugTextureProvider.Initialize();
        root = Node.CreateRoot(rootSize, OnNodeChanged);
        root.SetTileIndex(0, 0, 0);
        Node.Configure(splitRatio, mergeRatio, heightWeight, maxZ, magicNum);
    }

    void Update()
    {
        if (Camera.main != null)
        {
            Vector3 cameraPos = new Vector3(
                Camera.main.transform.position.x - terrainOffset.x,
                Camera.main.transform.position.y - terrainOffset.y,
                Camera.main.transform.position.z - terrainOffset.z
            );

            Node.UpdateAllLeavesState(cameraPos);

            // 更新调试信息
            totalNodes = CountTotalNodes(root);
            leafNodes = Node.currentAllLeaves.Count;

            // 每120帧清理一次无引用的纹理
            cleanupCounter++;
            if (cleanupCounter >= 120)
            {
                cleanupCounter = 0;
                CleanupUnusedTextures();
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || root == null) return;

        // 绘制所有节点
        root.DrawGizmos(terrainOffset);

        // 绘制叶子节点标签
        if (showLabels)
        {
            foreach (var node in Node.currentAllLeaves)
            {
                node.DrawLabel(terrainOffset);
            }
        }

        // 绘制相机位置
        if (Camera.main != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Camera.main.transform.position, 2f);
            Gizmos.DrawWireSphere(Camera.main.transform.position, 20f);
        }
    }

    void OnGUI()
    {
        // 显示调试信息
        GUI.Box(new Rect(10, 10, 250, 100), "Texture LOD Info");
        GUI.Label(new Rect(20, 30, 230, 20), $"Total Nodes: {totalNodes}");
        GUI.Label(new Rect(20, 50, 230, 20), $"Leaf Nodes: {leafNodes}");
        GUI.Label(new Rect(20, 70, 280, 20), $"Texture Count: {textureCache.Count}");
        GUI.Label(new Rect(20, 90, 280, 20), $"Texture Memory: {CalculateTotalTextureMemory() / 1048576f:F2} MB");
        GUI.Label(new Rect(20, 110, 230, 20), $"Root Size: {rootSize}");
        GUI.Label(new Rect(20, 130, 230, 20), $"Camera: {Camera.main.transform.position}");
    }

    private long CalculateTotalTextureMemory()
    {
        long total = 0;
        foreach (var texture in textureCache.Values)
        {
            if (texture != null && texture != Texture2D.blackTexture)
            {
                total += texture.width * texture.height * 4; // RGBA32
            }
        }
        return total;
    }

    // 新的 OnNodeChanged：接收 NodeChangeType 并按类型处理
    private void OnNodeChanged(Node node, NodeChangeType changeType)
    {
        if (node == null) return;

        switch (changeType)
        {
            case NodeChangeType.BecameLeaf:
                // 如果之前已经为这个 node 保存过 propertyBlock / texture，要先释放旧引用
                if (propertyBlocks.TryGetValue(node, out var oldBlock))
                {
                    propertyBlocks.Remove(node);
                    Texture2D oldTex = oldBlock.GetTexture("_MainTex") as Texture2D;
                    if (oldTex != null && oldTex != Texture2D.blackTexture)
                    {
                        DecrementTextureReference(oldTex);
                    }
                }

                // 创建或刷新 Quad（如果已经存在也先销毁再创建，避免重复）
                if (node.nodeObj != null)
                {
                    GameObject.Destroy(node.nodeObj);
                    node.nodeObj = null;
                }

                node.nodeObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                node.nodeObj.transform.position = new Vector3(node.x + node.size / 2f, 0, node.z + node.size / 2f);
                node.nodeObj.transform.localScale = new Vector3(node.size, node.size, 1);
                node.nodeObj.transform.rotation = Quaternion.Euler(90, 0, 0); // Quad 朝上

                if (sharedMat == null)
                {
                    sharedMat = new Material(Shader.Find("Unlit/Texture"));
                }

                Texture2D tileTex = LoadTileTexture(node.tileZ, node.tileX, node.tileY);
                var renderer = node.nodeObj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = sharedMat;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                if (tileTex != null)
                {
                    block.SetTexture("_MainTex", tileTex);
                }
                renderer.SetPropertyBlock(block);

                propertyBlocks[node] = block;
                break;

            case NodeChangeType.BecameBranch:
                // 节点由叶子变为分支：卸载该节点本身的资源（但子节点会另行发事件创建）
                UnloadNodeTextures(node, false); // 只卸载自身，不递归子节点
                break;

            case NodeChangeType.RemovedSubtree:
                // 父节点合并时发出的子树移除事件：递归卸载整棵子树
                UnloadNodeTextures(node, true);
                break;
        }
    }

    // recursive = true 时递归清理子节点（用于 RemovedSubtree）
    // recursive = false 时只清理当前节点本身（用于 BecameBranch）
    private void UnloadNodeTextures(Node node, bool recursive)
    {
        if (node == null)
        {
            Debug.LogWarning("尝试卸载 null 节点的纹理");
            return;
        }
        // 先清理当前节点的 property block（如果有）
        if (propertyBlocks.TryGetValue(node, out var block))
        {
            propertyBlocks.Remove(node);
            Texture2D texture = block.GetTexture("_MainTex") as Texture2D;
            if (texture != null && texture != Texture2D.blackTexture)
            {
                DecrementTextureReference(texture);
            }
        }
        // 销毁 GameObject
        if (node.nodeObj != null)
        {
            GameObject.Destroy(node.nodeObj);
            node.nodeObj = null;
        }

        if (recursive && node.children != null)
        {
            foreach (var child in node.children)
            {
                if (child == null)
                    continue;
                UnloadNodeTextures(child, true);
            }
        }
    }

    private void DecrementTextureReference(Texture2D texture)
    {
        if (texture == null || texture == Texture2D.blackTexture)
            return;

        string textureKey = texture.name;
        if (textureReferenceCount.ContainsKey(textureKey))
        {
            textureReferenceCount[textureKey]--;
            Debug.Log($"纹理引用减少: {texture.name} -> {textureReferenceCount[textureKey]}");

            // 只是减少引用计数，不立即销毁
            // 实际的清理在 CleanupUnusedTextures() 中进行
        }
        else
        {
            Debug.LogWarning($"纹理 {texture.name} 不在引用计数字典中");
        }
    }

    //private Texture2D LoadTileTexture(int z, int x, int y)
    //{
    //    string key = GetTextureKey(z, x, y);
    //    if (textureCache.TryGetValue(key, out Texture2D cachedTex))
    //    {
    //        if (textureReferenceCount.ContainsKey(key))
    //            textureReferenceCount[key]++;
    //        else
    //            textureReferenceCount[key] = 1;

    //        return cachedTex;
    //    }

    //    // 先尝试直接加载文件（并在加载成功后把它放入 cache）
    //    Texture2D tex = TryLoadTile(z, x, y);
    //    if (tex != null)
    //    {
    //        tex.name = key;
    //        textureCache[key] = tex;
    //        textureReferenceCount[key] = 1;
    //        return tex;
    //    }

    //    int fallbackZ = z;
    //    int fallbackX = x;
    //    int fallbackY = y;

    //    /*比如需要加载 “层级 5，坐标 (16,16)” 的高细节纹理，但加载失败。代码会自动尝试：
    //    先找 “层级 4，坐标(8, 8)” 的纹理（如果有，就用它裁剪出层级 5 所需的区域）；
    //    如果没有，再找 “层级 3，坐标(4, 4)” 的纹理；
    //    以此类推，直到找到可用的低层级纹理或层级降为 0。*/
    //    while (fallbackZ > 0)
    //    {
    //        fallbackZ -= 1;
    //        fallbackX /= 2;
    //        fallbackY /= 2;

    //        string fallbackKey = GetTextureKey(fallbackZ, fallbackX, fallbackY);
    //        if (textureCache.TryGetValue(fallbackKey, out Texture2D fallbackTex))
    //        {
    //            // 使用缓存的低级纹理进行裁剪（并生成一个专门的回退纹理 key）
    //            Texture2D newTex = CreateTextureFromFallback(fallbackTex, z, x, y, fallbackZ);
    //            return newTex;
    //        }
    //        else
    //        {
    //            Texture2D lowerTex = TryLoadTile(fallbackZ, fallbackX, fallbackY);
    //            if (lowerTex != null)
    //            {
    //                lowerTex.name = fallbackKey;
    //                textureCache[fallbackKey] = lowerTex;
    //                textureReferenceCount[fallbackKey] = 1;

    //                Texture2D newTex = CreateTextureFromFallback(lowerTex, z, x, y, fallbackZ);
    //                return newTex;
    //            }
    //        }
    //    }

    //    // 找不到任何图，返回内置黑贴图（不要把 blackTexture 加入 cache）
    //    return Texture2D.blackTexture;
    //}

    private Texture2D LoadTileTexture(int z, int x, int y)
    {
        string key = GetTextureKey(z, x, y);

        // 先查缓存
        if (textureCache.TryGetValue(key, out Texture2D cachedTex))
        {
            if (textureReferenceCount.ContainsKey(key))
                textureReferenceCount[key]++;
            else
                textureReferenceCount[key] = 1;

            return cachedTex;
        }

        // 启动异步加载（返回占位图，稍后替换）
        StartCoroutine(LoadTileTextureAsync(z, x, y, key));
        return Texture2D.blackTexture;
        //return null;
    }


    private IEnumerator LoadTileTextureAsync(int z, int x, int y, string key)
    {
        // 磁盘路径
        string path = $"file:///F:/otherdownload/AllWorld/{z}/{x}/{y}/tile.png";
        string bingstr = XyzToBing(z,x,y);
        string bingstrUrl = "https://ecn.t0.tiles.virtualearth.net/tiles/a"+bingstr+ ".jpeg?n=z&g=15368";

        using (var uwr = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(path))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                tex.name = key;

                textureCache[key] = tex;
                textureReferenceCount[key] = 1;

                ApplyTextureToNode(z, x, y, tex);
                yield break;
            }
        }

        // 主贴图加载失败，进入回退逻辑
        int fallbackZ = z;
        int fallbackX = x;
        int fallbackY = y;

        while (fallbackZ > 0)
        {
            fallbackZ -= 1;
            fallbackX /= 2;
            fallbackY /= 2;

            string fallbackKey = GetTextureKey(fallbackZ, fallbackX, fallbackY);
            if (textureCache.TryGetValue(fallbackKey, out Texture2D fallbackTex))
            {
                Texture2D newTex = CreateTextureFromFallback(fallbackTex, z, x, y, fallbackZ);
                ApplyTextureToNode(z, x, y, newTex);
                yield break;
            }
            else
            {
                string fbPath = $"file:///F:/otherdownload/AllWorld/{fallbackZ}/{fallbackX}/{fallbackY}/tile.png";
                string bingstr1 = XyzToBing(fallbackZ, fallbackX, fallbackY);
                string bingstr1Url = "https://ecn.t0.tiles.virtualearth.net/tiles/a" + bingstr1 + ".jpeg?n=z&g=15368";
                using (var uwr = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(fbPath))
                {
                    yield return uwr.SendWebRequest();
                    if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        Texture2D lowerTex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                        lowerTex.name = fallbackKey;

                        textureCache[fallbackKey] = lowerTex;
                        textureReferenceCount[fallbackKey] = 1;

                        Texture2D newTex = CreateTextureFromFallback(lowerTex, z, x, y, fallbackZ);
                        ApplyTextureToNode(z, x, y, newTex);
                        yield break;
                    }
                }
            }
        }
    }

    private void ApplyTextureToNode(int z, int x, int y, Texture2D tex)
    {
        foreach (var kvp in propertyBlocks)
        {
            if (kvp.Key.tileZ == z && kvp.Key.tileX == x && kvp.Key.tileY == y)
            {
                var renderer = kvp.Key.nodeObj.GetComponent<MeshRenderer>();
                MaterialPropertyBlock block = kvp.Value;
                block.SetTexture("_MainTex", tex);
                renderer.SetPropertyBlock(block);
                break;
            }
        }
    }

    public static string XyzToBing(int z, int x, int y)
    {
        StringBuilder result = new StringBuilder();
        double xCoord = x + 1;
        double yCoord = y + 1;
        int zAll = (int)Math.Pow(2, z);

        for (int i = 1; i <= z; i++)
        {
            double z0 = zAll / Math.Pow(2, i - 1);

            // 左上
            if (xCoord / z0 <= 0.5 && yCoord / z0 <= 0.5)
            {
                result.Append("0");
            }
            // 右上
            else if (xCoord / z0 > 0.5 && yCoord / z0 <= 0.5)
            {
                result.Append("1");
                xCoord -= z0 / 2;
            }
            // 左下
            else if (xCoord / z0 <= 0.5 && yCoord / z0 > 0.5)
            {
                result.Append("2");
                yCoord -= z0 / 2;
            }
            // 右下
            else if (xCoord / z0 > 0.5 && yCoord / z0 > 0.5)
            {
                result.Append("3");
                xCoord -= z0 / 2;
                yCoord -= z0 / 2;
            }
        }

        return result.ToString();
    }



    private Texture2D CreateTextureFromFallback(Texture2D sourceTex, int targetZ, int targetX, int targetY, int sourceZ)
    {
        string fallbackKey = GetTextureKey(targetZ, targetX, targetY) + $"_from_{sourceZ}";

        // 先检查是否已经有这个回退纹理
        if (textureCache.TryGetValue(fallbackKey, out Texture2D existingTex))
        {
            Debug.Log($"复用回退纹理: {fallbackKey}");

            if (textureReferenceCount.ContainsKey(fallbackKey))
                textureReferenceCount[fallbackKey]++;
            else
                textureReferenceCount[fallbackKey] = 1;
            return existingTex;
        }

        int factor = 1 << (targetZ - sourceZ);
        int subSize = 256 / factor;
        int px = (targetX % factor) * subSize;
        int py = (factor - 1 - (targetY % factor)) * subSize;

        Color[] pixels = sourceTex.GetPixels(px, py, subSize, subSize);

        Texture2D newTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        // 使用最近邻插值（性能更好）
        Color[] resizedPixels = new Color[256 * 256];
        float scale = (float)subSize / 256f;

        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                int srcX = Mathf.FloorToInt(x * scale);
                int srcY = Mathf.FloorToInt(y * scale);
                srcX = Mathf.Clamp(srcX, 0, subSize - 1);
                srcY = Mathf.Clamp(srcY, 0, subSize - 1);

                resizedPixels[y * 256 + x] = pixels[srcY * subSize + srcX];
            }
        }

        newTex.SetPixels(resizedPixels);
        newTex.Apply();
        newTex.name = fallbackKey;
        // 缓存这个回退纹理
        textureCache[fallbackKey] = newTex;
        textureReferenceCount[fallbackKey] = 1;

        Debug.Log($"创建回退纹理: {newTex.name}");

        return newTex;
    }

    private Texture2D TryLoadTile(int z, int x, int y)
    {
        string key = GetTextureKey(z, x, y);
        string externalPath = $"F:/otherdownload/AllWorld/{z}/{x}/{y}/tile.png";
        if (System.IO.File.Exists(externalPath))
        {
            try
            {
                byte[] fileData = System.IO.File.ReadAllBytes(externalPath);
                Texture2D tex = new Texture2D(256, 256);
                tex.LoadImage(fileData);
                tex.name = key;
                Debug.Log($"成功加载外部纹理: {externalPath}");
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载外部纹理失败: {externalPath}, 错误: {e.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogWarning($"外部纹理不存在: {externalPath}");
            return null;
        }
    }

    private int CountTotalNodes(Node node)
    {
        if (node == null) return 0;
        if (node.isLeaf) return 1;

        int count = 1;
        foreach (var child in node.children)
        {
            count += CountTotalNodes(child);
        }
        return count;
    }

    private void CleanupUnusedTextures()
    {
        int removedCount = 0;
        long freedMemory = 0;

        // 创建临时列表来收集需要清理的项目
        var keysToRemove = new List<string>();

        try
        {
            // 第一步：收集需要清理的纹理键
            foreach (var kvp in textureReferenceCount)
            {
                if (kvp.Value <= 0)
                {
                    // 查找对应的纹理缓存项
                    if (textureCache.ContainsKey(kvp.Key))
                    {
                        keysToRemove.Add(kvp.Key);
                        Texture2D texture = textureCache[kvp.Key];
                        if (texture != null)
                        {
                            freedMemory += texture.width * texture.height * 4;
                        }
                    }
                }
            }

            // 第二步：执行清理
            foreach (var key in keysToRemove)
            {
                if (textureCache.Remove(key, out Texture2D texture))
                {
                    if (texture != null)
                    {
                        Destroy(texture);
                        removedCount++;
                    }
                }
                textureReferenceCount.Remove(key);
            }

            if (removedCount > 0)
            {
                Debug.Log($"纹理清理: {removedCount}个纹理, {freedMemory / 1048576}MB");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"清理纹理时发生错误: {e.Message}");
        }

        CheckDictionaryConsistency();
    }

    private void CheckDictionaryConsistency()
    {
        // 检查textureCache中的所有纹理是否都在textureReferenceCount中
        foreach (var kvp in textureCache)
        {
            string textureKey = kvp.Key;
            if (!textureReferenceCount.ContainsKey(textureKey) && kvp.Value != Texture2D.blackTexture)
            {
                Debug.LogWarning($"纹理 {textureKey} 在缓存中但不在引用字典");
                textureReferenceCount[textureKey] = 0; // 添加并标记为0，下次清理
            }
        }

        // 检查textureReferenceCount中的纹理是否都在textureCache中
        var refKeysToRemove = new List<string>();
        foreach (var key in textureReferenceCount.Keys)
        {
            if (!textureCache.ContainsKey(key))
            {
                Debug.LogWarning($"纹理键 {key} 在引用字典中但不在缓存");
                refKeysToRemove.Add(key);
            }
        }

        foreach (var key in refKeysToRemove)
        {
            textureReferenceCount.Remove(key);
        }
    }
}

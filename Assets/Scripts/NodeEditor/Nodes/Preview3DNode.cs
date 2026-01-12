using System;
using System.Collections;
using UnityEngine;
using GLTFast;

namespace AIPipeline.Nodes
{
    /// <summary>
    /// 3D 预览节点 - 显示并可实例化模型
    /// </summary>
    public class Preview3DNode : PipelineNode
    {
        [Header("Preview Settings")]
        public Vector3 previewPosition = Vector3.zero;
        public Vector3 previewScale = Vector3.one;
        public bool autoInstantiate = true;
        
        [Header("Runtime")]
        public GameObject instantiatedModel;
        
        public override PortType? InputType => PortType.Model3D;
        public override PortType? OutputType => null; // 终端节点
        
        private void Awake()
        {
            nodeName = "3D Preview";
            nodeColor = new Color(0.6f, 1f, 0.6f); // 浅绿色
        }
        
        public override void Execute(Action<object> onComplete, Action<string> onError)
        {
            byte[] glbData = GetInputData<byte[]>();
            if (glbData == null || glbData.Length == 0)
            {
                onError?.Invoke("No GLB data received");
                return;
            }
            
            StartCoroutine(LoadAndInstantiate(glbData, onComplete, onError));
        }
        
        private IEnumerator LoadAndInstantiate(byte[] glbData, Action<object> onComplete, Action<string> onError)
        {
            var gltf = new GltfImport();
            
            var loadTask = gltf.LoadGltfBinary(glbData);
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (!loadTask.Result)
            {
                onError?.Invoke("Failed to parse GLB data");
                yield break;
            }
            
            // 清除之前的模型
            if (instantiatedModel != null)
            {
                Destroy(instantiatedModel);
            }
            
            // 创建新模型
            instantiatedModel = new GameObject("PreviewModel_" + DateTime.Now.Ticks);
            
            if (autoInstantiate)
            {
                // 在相机前方生成
                if (Camera.main != null)
                {
                    previewPosition = Camera.main.transform.position + 
                                     Camera.main.transform.forward * 3f;
                }
            }
            
            instantiatedModel.transform.position = previewPosition;
            instantiatedModel.transform.localScale = previewScale;
            
            var instantiateTask = gltf.InstantiateMainSceneAsync(instantiatedModel.transform);
            while (!instantiateTask.IsCompleted)
            {
                yield return null;
            }
            
            if (!instantiateTask.Result)
            {
                Destroy(instantiatedModel);
                onError?.Invoke("Failed to instantiate model");
                yield break;
            }
            
            Debug.Log($"[Preview3D] Model instantiated at {previewPosition}");
            onComplete?.Invoke(instantiatedModel);
        }
    }
}

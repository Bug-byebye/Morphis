using System;
using System.Collections.Generic;
using UnityEngine;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 单个世界物体的数据
    /// </summary>
    [Serializable]
    public class WorldObjectData
    {
        public string object_id;
        public string prefab_id;
        
        [SerializeField]
        private float pos_x, pos_y, pos_z;
        [SerializeField]
        private float rot_x, rot_y, rot_z, rot_w;
        [SerializeField]
        private float scale_x, scale_y, scale_z;

        public Vector3 position
        {
            get => new Vector3(pos_x, pos_y, pos_z);
            set
            {
                pos_x = value.x;
                pos_y = value.y;
                pos_z = value.z;
            }
        }

        public Quaternion rotation
        {
            get => new Quaternion(rot_x, rot_y, rot_z, rot_w);
            set
            {
                rot_x = value.x;
                rot_y = value.y;
                rot_z = value.z;
                rot_w = value.w;
            }
        }

        public Vector3 scale
        {
            get => new Vector3(scale_x, scale_y, scale_z);
            set
            {
                scale_x = value.x;
                scale_y = value.y;
                scale_z = value.z;
            }
        }

        public WorldObjectData()
        {
            object_id = Guid.NewGuid().ToString();
        }

        public WorldObjectData(string prefabId, Vector3 pos, Quaternion rot, Vector3 scl)
        {
            object_id = Guid.NewGuid().ToString();
            prefab_id = prefabId;
            position = pos;
            rotation = rot;
            scale = scl;
        }
    }

    /// <summary>
    /// 世界快照数据
    /// </summary>
    [Serializable]
    public class WorldSnapshot
    {
        public string world_id;
        public int version;
        public List<WorldObjectData> objects;

        public WorldSnapshot()
        {
            objects = new List<WorldObjectData>();
            version = 1;
        }

        public WorldSnapshot(string worldId) : this()
        {
            world_id = worldId;
        }

        /// <summary>
        /// 递增版本号
        /// </summary>
        public void IncrementVersion()
        {
            version++;
        }
    }
}

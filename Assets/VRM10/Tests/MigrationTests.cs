﻿using System.IO;
using NUnit.Framework;
using UnityEngine;
using UniJSON;
using System;
using UniGLTF;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace UniVRM10
{
    public class MigrationTests
    {
        static string AliciaPath
        {
            get
            {
                return Path.GetFullPath(Application.dataPath + "/../Tests/Models/Alicia_vrm-0.51/AliciaSolid_vrm-0.51.vrm")
                    .Replace("\\", "/");
            }
        }

        static JsonNode GetVRM0(byte[] bytes)
        {
            using (var glb = new GlbBinaryParser(bytes, "vrm0").Parse())
            {
                var json = glb.Json.ParseAsJson();
                return json["extensions"]["VRM"];
            }
        }

        T GetExtension<T>(UniGLTF.glTFExtension extensions, UniJSON.Utf8String key, Func<JsonNode, T> deserializer)
        {
            if (extensions is UniGLTF.glTFExtensionImport import)
            {
                foreach (var kv in import.ObjectItems())
                {
                    if (kv.Key.GetUtf8String() == key)
                    {
                        return deserializer(kv.Value);
                    }
                }
            }

            return default;
        }

        [Test]
        public void Migrate0to1()
        {
            var vrm0Bytes = File.ReadAllBytes(AliciaPath);
            var vrm0Json = GetVRM0(vrm0Bytes);

            var vrm1 = MigrationVrm.Migrate(vrm0Bytes);
            using (var glb = new GlbBinaryParser(vrm1, "vrm1").Parse())
            {
                var json = glb.Json.ParseAsJson();
                var gltf = UniGLTF.GltfDeserializer.Deserialize(json);

                MigrationVrm.Check(vrm0Json, GetExtension(gltf.extensions, UniGLTF.Extensions.VRMC_vrm.GltfDeserializer.ExtensionNameUtf8,
                    UniGLTF.Extensions.VRMC_vrm.GltfDeserializer.Deserialize), MigrationVrm.CreateMeshToNode(gltf));
                MigrationVrm.Check(vrm0Json, GetExtension(gltf.extensions, UniGLTF.Extensions.VRMC_springBone.GltfDeserializer.ExtensionNameUtf8,
                    UniGLTF.Extensions.VRMC_springBone.GltfDeserializer.Deserialize), gltf.nodes);
            }
        }

        const float EPS = 1e-4f;

        static bool Nearly(float l, float r)
        {
            return Mathf.Abs(l - r) <= EPS;
        }

        static bool Nearly(Vector3 l, Vector3 r)
        {
            if (!Nearly(l.x, r.x)) return false;
            if (!Nearly(l.y, r.y)) return false;
            if (!Nearly(l.z, r.z)) return false;
            return true;
        }

        [Test]
        public void RotateY180Test()
        {
            var euler = new Vector3(0, 10, 20);
            var r = Quaternion.Euler(euler);
            var node = new glTFNode
            {
                translation = new float[] { 1, 2, 3 },
                // rotation = new float[] { r.x, r.y, r.z, r.w },
                scale = new float[] { 1, 2, 3 },
            };
            RotateY180.Rotate(node);

            Assert.AreEqual(new Vector3(-1, 2, -3), node.translation.ToVector3());
            Assert.AreEqual(new Vector3(1, 2, 3), node.scale.ToVector3());

            // var result = node.rotation.ToQuaternion().ToUnityQuaternion().eulerAngles;
            // Debug.LogFormat($"{result}");

            // Assert.True(Nearly(0, result.x));
            // Assert.True(Nearly(10, result.y));
            // Assert.True(Nearly(20, result.z));
        }

        [Test]
        public void UnityEngineMatrixTest()
        {
            var u = new UnityEngine.Matrix4x4();
            u.m00 = 0;
            u.m01 = 1;
            u.m02 = 2;
            u.m03 = 3;
            u.m10 = 4;
            u.m11 = 5;
            u.m12 = 6;
            u.m13 = 7;
            u.m20 = 8;
            u.m21 = 9;
            u.m22 = 10;
            u.m23 = 11;
            u.m30 = 12;
            u.m31 = 13;
            u.m32 = 14;
            u.m33 = 15;
            Assert.AreEqual(new UnityEngine.Vector4(0, 1, 2, 3), u.GetRow(0));
            var bytes = new Byte[64];
            SafeMarshalCopy.CopyArrayToToBytes(new[] { u }, new ArraySegment<byte>(bytes));
            Assert.AreEqual(1.0f, BitConverter.ToSingle(bytes, 16));
        }

        [Test]
        public void NumericMatrixTest()
        {
            var u = new System.Numerics.Matrix4x4();
            u.M11 = 0;
            u.M12 = 1;
            u.M13 = 2;
            u.M14 = 3;
            u.M21 = 4;
            u.M22 = 5;
            u.M23 = 6;
            u.M24 = 7;
            u.M31 = 8;
            u.M32 = 9;
            u.M33 = 10;
            u.M34 = 11;
            u.M41 = 12;
            u.M42 = 13;
            u.M43 = 14;
            u.M44 = 15;
            var bytes = new Byte[64];
            SafeMarshalCopy.CopyArrayToToBytes(new[] { u }, new ArraySegment<byte>(bytes));
            Assert.AreEqual(1.0f, BitConverter.ToSingle(bytes, 4));
        }

        static IEnumerable<FileInfo> EnumerateGltfFiles(DirectoryInfo dir)
        {
            if (dir.Name == ".git")
            {
                yield break;
            }

            foreach (var child in dir.EnumerateDirectories())
            {
                foreach (var x in EnumerateGltfFiles(child))
                {
                    yield return x;
                }
            }

            foreach (var child in dir.EnumerateFiles())
            {
                switch (child.Extension.ToLower())
                {
                    case ".vrm":
                        yield return child;
                        break;
                }
            }
        }

        [Test]
        public void Migrate_VrmTestModels()
        {
            var env = System.Environment.GetEnvironmentVariable("VRM_TEST_MODELS");
            if (string.IsNullOrEmpty(env))
            {
                return;
            }
            var root = new DirectoryInfo(env);
            if (!root.Exists)
            {
                return;
            }

            foreach (var gltf in EnumerateGltfFiles(root))
            {
                try
                {
                    Vrm10.LoadPathAsync(gltf.FullName, true, false).Wait();
                }
                catch (UnNormalizedException)
                {
                    Debug.LogWarning($"[Not Normalized] {gltf}");
                }
            }
        }

        /// <summary>
        /// migration で x が反転することを確認
        /// </summary>
        [Test]
        public void Migrate_SpringBoneTest()
        {
            //
            // vrm0 のオリジナルの値
            //
            var VALUE = new Vector3(-0.0359970331f, -0.0188314915f, 0.00566166639f);
            var bytes0 = File.ReadAllBytes(AliciaPath);
            int groupIndex = default;
            using (var data0 = new GlbLowLevelParser(AliciaPath, bytes0).Parse())
            {
                var json0 = data0.Json.ParseAsJson();
                groupIndex = json0["extensions"]["VRM"]["secondaryAnimation"]["boneGroups"][0]["colliderGroups"][0].GetInt32();
                var x = json0["extensions"]["VRM"]["secondaryAnimation"]["colliderGroups"][groupIndex]["colliders"][0]["offset"]["x"].GetSingle();
                var y = json0["extensions"]["VRM"]["secondaryAnimation"]["colliderGroups"][groupIndex]["colliders"][0]["offset"]["y"].GetSingle();
                var z = json0["extensions"]["VRM"]["secondaryAnimation"]["colliderGroups"][groupIndex]["colliders"][0]["offset"]["z"].GetSingle();
                Assert.AreEqual(VALUE.x, x);
                Assert.AreEqual(VALUE.y, y);
                Assert.AreEqual(VALUE.z, z);
            }

            //
            // vrm1 に migrate
            //
            var bytes1 = MigrationVrm.Migrate(bytes0);
            using (var data1 = new GlbLowLevelParser(AliciaPath, bytes1).Parse())
            {
                Assert.True(UniGLTF.Extensions.VRMC_springBone.GltfDeserializer.TryGet(data1.GLTF.extensions, out UniGLTF.Extensions.VRMC_springBone.VRMC_springBone springBone));
                var spring = springBone.Springs[0];
                // var colliderNodeIndex = spring.ColliderGroups[0];
                // x軸だけが反転する

                var colliderIndex = 0;
                for (int i = 0; i < groupIndex; ++i)
                {
                    colliderIndex += springBone.ColliderGroups[i].Colliders.Length;
                }

                Assert.AreEqual(-VALUE.x, springBone.Colliders[colliderIndex].Shape.Sphere.Offset[0]);
                Assert.AreEqual(VALUE.y, springBone.Colliders[colliderIndex].Shape.Sphere.Offset[1]);
                Assert.AreEqual(VALUE.z, springBone.Colliders[colliderIndex].Shape.Sphere.Offset[2]);
            }
        }

        [Test]
        public void MigrateMeta()
        {
            using (var data = new GlbFileParser(AliciaPath).Parse())
            using (var migrated = Vrm10Data.Migrate(data, out Vrm10Data vrm, out MigrationData migration))
            {
                Assert.NotNull(vrm);
                Assert.NotNull(migration);
            }
        }
    }
}

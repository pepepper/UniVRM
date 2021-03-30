﻿#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif


namespace UniVRM10
{
    [ScriptedImporter(1, "vrm")]
    public class VrmScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            VrmScriptedImporterImpl.Import(this, ctx);
        }
    }
}

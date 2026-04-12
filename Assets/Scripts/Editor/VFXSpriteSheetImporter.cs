using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.Collections.Generic;
using System.IO;

// Batch-imports all Texture2D files in a folder as sliced sprite sheets.
// Open via: Pokemon → Import VFX Sprite Sheets
// Uses ISpriteEditorDataProvider — the same API the Sprite Editor uses internally.

public class VFXSpriteSheetImporter : EditorWindow
{
    private string     targetFolder = "Assets/VFX/Sprites";
    private int        cellWidth    = 64;
    private int        cellHeight   = 64;
    private int        paddingX     = 0;
    private int        paddingY     = 0;
    private FilterMode filterMode   = FilterMode.Bilinear;
    private bool       pixelArt     = false;

    [MenuItem("Pokemon/Import VFX Sprite Sheets")]
    public static void ShowWindow()
        => GetWindow<VFXSpriteSheetImporter>("VFX Sheet Importer");

    private void OnGUI()
    {
        GUILayout.Label("Batch VFX Sprite Sheet Importer", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        targetFolder = EditorGUILayout.TextField(new GUIContent("Folder", "Path inside Assets/ that holds your sprite sheet PNGs"), targetFolder);

        EditorGUILayout.Space(4);
        GUILayout.Label("Frame Cell Size", EditorStyles.boldLabel);
        cellWidth  = EditorGUILayout.IntField(new GUIContent("Cell Width  (px)", "Width of one animation frame"), cellWidth);
        cellHeight = EditorGUILayout.IntField(new GUIContent("Cell Height (px)", "Height of one animation frame"), cellHeight);
        paddingX   = EditorGUILayout.IntField(new GUIContent("Padding X   (px)", "Horizontal gap between frames (usually 0)"), paddingX);
        paddingY   = EditorGUILayout.IntField(new GUIContent("Padding Y   (px)", "Vertical gap between frames (usually 0)"), paddingY);

        EditorGUILayout.Space(4);
        GUILayout.Label("Import Settings", EditorStyles.boldLabel);
        pixelArt = EditorGUILayout.Toggle(new GUIContent("Pixel Art Mode", "Point filter + no compression"), pixelArt);
        if (!pixelArt)
            filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", filterMode);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Every Texture2D in the folder will be set to Sprite (Multiple) and grid-sliced " +
            "using the cell size above.\n\n" +
            "Tip: open one sheet in the Sprite Editor to verify the cell size before running.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Import & Slice All", GUILayout.Height(30)))
            ImportAll();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Count sheets in folder"))
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { targetFolder });
            EditorUtility.DisplayDialog("Count", $"Found {guids.Length} textures in '{targetFolder}'.", "OK");
        }
    }

    private void ImportAll()
    {
        if (cellWidth <= 0 || cellHeight <= 0)
        {
            EditorUtility.DisplayDialog("Error", "Cell width and height must be greater than 0.", "OK");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { targetFolder });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Nothing found", $"No textures found in '{targetFolder}'.", "OK");
            return;
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();

        int processed = 0;
        int skipped   = 0;

        try
        {
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                EditorUtility.DisplayProgressBar(
                    "Importing Sprite Sheets",
                    Path.GetFileName(path),
                    (float)g / guids.Length);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { skipped++; continue; }

                // Step 1 — set import settings and do a first reimport so the
                // texture asset exists with correct type before we set sprite rects
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Multiple;
                importer.mipmapEnabled       = false;
                importer.alphaIsTransparency = true;

                if (pixelArt)
                {
                    importer.filterMode         = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                }
                else
                {
                    importer.filterMode = filterMode;
                }

                importer.SaveAndReimport();

                // Step 2 — load the now-imported texture to read dimensions
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { skipped++; continue; }

                int texW  = tex.width;
                int texH  = tex.height;
                int stepX = cellWidth  + paddingX;
                int stepY = cellHeight + paddingY;
                int cols  = texW / stepX;
                int rows  = texH / stepY;

                if (cols == 0 || rows == 0) { skipped++; continue; }

                // Step 3 — build grid rects and apply via ISpriteEditorDataProvider
                // This is the same API the Sprite Editor uses — guaranteed to stick
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                if (dataProvider == null) { skipped++; continue; }

                dataProvider.InitSpriteEditorDataProvider();

                string baseName = Path.GetFileNameWithoutExtension(path);
                var    rects    = new List<SpriteRect>();
                int    idx      = 0;

                // Unity UV origin is bottom-left, so iterate rows bottom-up
                for (int row = rows - 1; row >= 0; row--)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        rects.Add(new SpriteRect
                        {
                            name      = baseName + "_" + idx,
                            rect      = new Rect(col * stepX, row * stepY, cellWidth, cellHeight),
                            pivot     = new Vector2(0.5f, 0.5f),
                            alignment = SpriteAlignment.Center,
                            spriteID  = GUID.Generate()
                        });
                        idx++;
                    }
                }

                dataProvider.SetSpriteRects(rects.ToArray());
                dataProvider.Apply();

                // SaveAndReimport writes the rects to the .meta and reimports
                (dataProvider.targetObject as AssetImporter)?.SaveAndReimport();
                processed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Done",
            $"Processed: {processed} sheets\nSkipped:   {skipped}\n\nFolder: {targetFolder}",
            "OK");

        Debug.Log($"[VFX Importer] Done — {processed} imported, {skipped} skipped.");
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum SmoothedNormalsChannel
{
	VertexColor,
	Tangent,
	UV1,
	UV2,
	UV3,
	UV4
}



public class SmoothedNormalsUtility : EditorWindow
{
    #region GUI

    private string mFilePath = "";

	private string mExtraFileName = "_SmoothedNormals";
	private Vector2 mScroll;

	[MenuItem("Tools/Smoothed Normals Utility")]
	static void OpenTool()
	{
		GetWindow();
	}

	private static SmoothedNormalsUtility GetWindow()
	{
		var window = GetWindow<SmoothedNormalsUtility>(true, "Smoothed Normals Utility", true);
		window.minSize = new Vector2(400f, 400f);
		window.maxSize = new Vector2(400f, 5000f);
		return window;
	}

	private void OnFocus()
	{
		mMeshes = GetSelectedMeshes();
	}

	private void OnSelectionChange()
	{
		mMeshes = GetSelectedMeshes();
		Repaint();
	}

	private void OnGUI()
	{
		GUI_SelectMesh();
		GUIHelper.SeparatorSimple();
		GUI_SelectSaveChannel();
		GUIHelper.SeparatorSimple();
		GUI_SeletSavePath();
	}

	private void GUI_SelectMesh() 
	{
		if (mMeshes != null && mMeshes.Count > 0)
		{
			mScroll = EditorGUILayout.BeginScrollView(mScroll);
			foreach (var sm in mMeshes.Values)
			{
				GUILayout.Space(2);
				GUILayout.BeginHorizontal();
				var label = sm.mesh.name;
				if (!string.IsNullOrEmpty(mExtraFileName) && label.Contains(mExtraFileName))
				{
					label = label.Replace(mExtraFileName, mExtraFileName);
				}
				GUILayout.Label(label, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(260));
				GUILayout.EndHorizontal();
				GUILayout.Space(2);
				GUIHelper.SeparatorSimple();
			}
			EditorGUILayout.EndScrollView();
			GUILayout.FlexibleSpace();

			if (GUILayout.Button(mMeshes.Count == 1 ? "Generate Smoothed Mesh" : "Generate Smoothed Meshes", GUILayout.Height(30)))
			{
				try
				{
					var selection = new List<Object>();
					float progress = 1;
					float total = mMeshes.Count;
					foreach (var sm in mMeshes.Values)
					{
						if (sm == null)
							continue;

						EditorUtility.DisplayProgressBar("Hold On", (mMeshes.Count > 1 ?
							"Generating Smoothed Meshes:\n" : "Generating Smoothed Mesh:\n") + sm.Name, progress / total);
						progress++;
						Object o = CreateSmoothedMeshAsset(sm);
						if (o != null)
							selection.Add(o);
					}
					Selection.objects = selection.ToArray();
				}
				finally
				{
					EditorUtility.ClearProgressBar();
				}
			}
		}
		else
		{
			EditorGUILayout.HelpBox("Select Mesh/Model/MeshFilter/SkinMeshRenderer to create a smoothed normals version mesh.", MessageType.Info);
			GUILayout.FlexibleSpace();
		}
	}

	private void GUI_SelectSaveChannel() 
	{
		saveChannel = (SmoothedNormalsChannel)EditorGUILayout.EnumPopup("Save Channel", saveChannel);
		if (saveChannel == SmoothedNormalsChannel.UV1 ||
			saveChannel == SmoothedNormalsChannel.UV2 ||
			saveChannel == SmoothedNormalsChannel.UV3 ||
			saveChannel == SmoothedNormalsChannel.UV4)
		{
			saveInTangentSpace = EditorGUILayout.Toggle("Save In TangentSpace", saveInTangentSpace);
		}
	}

	private void GUI_SeletSavePath() 
	{
		mExtraFileName = EditorGUILayout.TextField("Extra Name", mExtraFileName);
		EditorGUILayout.BeginHorizontal();
		mFilePath = EditorGUILayout.TextField("Asssets Path", mFilePath);
		if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
		{
			string outputPath = GUIHelper.SelectPath("Choose custom output directory for generated smoothed meshes", mFilePath);
			if (!string.IsNullOrEmpty(outputPath))
			{
				mFilePath = outputPath;
			}
		}
		EditorGUILayout.EndHorizontal();
	}

    #endregion

    #region Mesh
    private class SelectedMesh
	{
		public Mesh mesh;

		// �Ƿ���Asset��Դ
		public bool isAssets;

		// ���ѡ�����MeshFilter����SkinnedMeshRenderer���Լ��뵽����б�������֮��ֱ���Զ��滻
		private List<Object> _associatedObjects = new List<Object>();
		public Object[] associatedObjects
		{
			get
			{
				if (_associatedObjects.Count == 0) return null;
				return _associatedObjects.ToArray();
			}
		}

		public string Name { get { return mesh.name; }  }

		public SelectedMesh(Mesh _mesh, bool _isAssets, Object _assoObj = null)
		{
			mesh = _mesh;

			isAssets = _isAssets;

			AddAssociatedObject(_assoObj);
		}

		public void AddAssociatedObject(Object _assoObj)
		{
			if (_assoObj != null)
			{
				_associatedObjects.Add(_assoObj);
			}
		}
	}

	private Dictionary<Mesh, SelectedMesh> mMeshes;

	private SmoothedNormalsChannel saveChannel = SmoothedNormalsChannel.Tangent;

	private bool saveInTangentSpace = false;

	private Dictionary<Mesh, SelectedMesh> GetSelectedMeshes()
	{
		Dictionary<Mesh, SelectedMesh> meshDict = new Dictionary<Mesh, SelectedMesh>();

		foreach (Object o in Selection.objects)
		{
			bool isProjectAsset = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o));

			//Assets from Project
			if (o is Mesh && !meshDict.ContainsKey(o as Mesh))
			{
				if ((o as Mesh) != null)
				{
					SelectedMesh sm = GetMeshToAdd(o as Mesh, isProjectAsset);
					if (sm != null)
						meshDict.Add(o as Mesh, sm);
				}
			}
			else if (o is GameObject && isProjectAsset)
			{
				string path = AssetDatabase.GetAssetPath(o);
				Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
				foreach (Object asset in allAssets)
				{
					if (asset is Mesh && !meshDict.ContainsKey(asset as Mesh))
					{
						if ((asset as Mesh) != null)
						{
							var sm = GetMeshToAdd(asset as Mesh, isProjectAsset);
							if (sm.mesh != null)
								meshDict.Add(asset as Mesh, sm);
						}
					}
				}
			}
			//Assets from Hierarchy
			else if (o is GameObject && !isProjectAsset)
			{
				SkinnedMeshRenderer[] skinnedMeshRenderers = (o as GameObject).GetComponentsInChildren<SkinnedMeshRenderer>();
				foreach (SkinnedMeshRenderer renderer in skinnedMeshRenderers)
				{
					if (renderer.sharedMesh != null)
					{
						if (meshDict.ContainsKey(renderer.sharedMesh))
						{
							var sm = meshDict[renderer.sharedMesh];
							sm.AddAssociatedObject(renderer);
						}
						else
						{
							if (renderer.sharedMesh.name.Contains(mExtraFileName))
							{
								meshDict.Add(renderer.sharedMesh, new SelectedMesh(renderer.sharedMesh, false));
							}
							else
							{
								if (renderer.sharedMesh != null)
								{
									var sm = GetMeshToAdd(renderer.sharedMesh, true, renderer);
									if (sm.mesh != null)
										meshDict.Add(renderer.sharedMesh, sm);
								}
							}
						}
					}
				}

				MeshFilter[] meshFilters = (o as GameObject).GetComponentsInChildren<MeshFilter>();
				foreach (MeshFilter filter in meshFilters)
				{
					if (filter.sharedMesh != null)
					{
						if (meshDict.ContainsKey(filter.sharedMesh))
						{
							var sm = meshDict[filter.sharedMesh];
							sm.AddAssociatedObject(filter);
						}
						else
						{
							if (filter.sharedMesh.name.Contains(mExtraFileName))
							{
								meshDict.Add(filter.sharedMesh, new SelectedMesh(filter.sharedMesh, false));
							}
							else
							{
								if (filter.sharedMesh != null)
								{
									var sm = GetMeshToAdd(filter.sharedMesh, true, filter);
									if (sm.mesh != null)
										meshDict.Add(filter.sharedMesh, sm);
								}
							}
						}
					}
				}
			}
		}

		return meshDict;
	}

	private SelectedMesh GetMeshToAdd(Mesh mesh, bool isProjectAsset, Object _assoObj = null)
	{
		var meshPath = AssetDatabase.GetAssetPath(mesh);
		var meshAsset = AssetDatabase.LoadAssetAtPath(meshPath, typeof(Mesh)) as Mesh;
		//If null, it can be a built-in Unity mesh
		if (meshAsset == null)
		{
			return new SelectedMesh(mesh, isProjectAsset, _assoObj);
		}
		var meshName = mesh.name;
		if (!AssetDatabase.IsMainAsset(meshAsset))
		{
			var main = AssetDatabase.LoadMainAssetAtPath(meshPath);
			meshName = main.name + " - " + meshName + "_" + mesh.GetInstanceID();
		}

		var sm = new SelectedMesh(mesh, isProjectAsset, _assoObj);
		return sm;
	}

	private Mesh CreateSmoothedMeshAsset(SelectedMesh originalMesh)
	{
		// �����洢·���������Ƿ����Ѿ����ɵ�Mesh
		string savePath = Application.dataPath + "/" + mFilePath;
		if (!Directory.Exists(savePath)) 
			Directory.CreateDirectory(savePath);
		string assetPath = "Assets/" + mFilePath + "/";
		string originalMeshName = originalMesh.Name;
		string newAssetName = originalMeshName + mExtraFileName + ".asset";
		if (originalMeshName.Contains(mExtraFileName))
		{
			newAssetName = originalMeshName + ".asset";
		}
		assetPath += newAssetName;
		Mesh existingAsset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Mesh)) as Mesh;
		bool assetExists = (existingAsset != null) && originalMesh.isAssets;
		if (assetExists)
		{
			originalMesh.mesh = existingAsset;
		}

		// ����Mesh(������Ѿ������˵��Ǿ�ֻ��Ҫ�޸ģ�������Ҫnew Mesh();
		Mesh newMesh = CreateSmoothedNormalsMesh(originalMesh.mesh, saveChannel, 
			!originalMesh.isAssets || !(originalMesh.isAssets && assetExists));

		if (newMesh == null)
		{
			ShowNotification(new GUIContent("Couldn't generate the mesh for:" + originalMesh.Name));
		}
		else 
		{
			// ���ø�ָ�������岢����
			if (originalMesh.associatedObjects != null)
			{
				Undo.RecordObjects(originalMesh.associatedObjects, "Assign Smoothed Mesh to Selection");

				foreach (var o in originalMesh.associatedObjects)
				{
					if (o is SkinnedMeshRenderer)
					{
						(o as SkinnedMeshRenderer).sharedMesh = newMesh;
					}
					else if (o is MeshFilter)
					{
						(o as MeshFilter).sharedMesh = newMesh;
					}
					else
					{
						Debug.LogWarning("Unrecognized AssociatedObject: " + o + ",Type: " + o.GetType());
					}
					EditorUtility.SetDirty(o);
				}
			}

			if (!assetExists)
				AssetDatabase.CreateAsset(newMesh, assetPath);
		}

		return newMesh;
	}

	public Mesh CreateSmoothedNormalsMesh(Mesh originMesh, SmoothedNormalsChannel saveChannel, bool createMesh)
	{
		if (originMesh == null)
		{
			Debug.LogWarning("Supplied OriginalMesh is null, can't create smooth normals version.");
			return null;
		}

		//Create new mesh
		Mesh newMesh = createMesh ? new Mesh() : originMesh;
		if (createMesh)
		{ 
			newMesh.vertices = originMesh.vertices;
			newMesh.normals = originMesh.normals;
			newMesh.tangents = originMesh.tangents;
			newMesh.uv = originMesh.uv;
			newMesh.uv2 = originMesh.uv2;
			newMesh.uv3 = originMesh.uv3;
			newMesh.uv4 = originMesh.uv4;
			newMesh.colors32 = originMesh.colors32;
			newMesh.triangles = originMesh.triangles;
			newMesh.bindposes = originMesh.bindposes;
			newMesh.boneWeights = originMesh.boneWeights;

			if (originMesh.blendShapeCount > 0)
				CopyBlendShapes(originMesh, newMesh);

			newMesh.subMeshCount = originMesh.subMeshCount;
			if (newMesh.subMeshCount > 1)
				for (var i = 0; i < newMesh.subMeshCount; i++)
					newMesh.SetTriangles(originMesh.GetTriangles(i), i);
		}

		//Calculate smoothed normals
		var averageNormalsHash = new Dictionary<Vector3, Vector3>();
		for (var i = 0; i < newMesh.vertexCount; i++)
		{
			if (!averageNormalsHash.ContainsKey(newMesh.vertices[i]))
				averageNormalsHash.Add(newMesh.vertices[i], newMesh.normals[i]);
			else
				averageNormalsHash[newMesh.vertices[i]] = 
					(averageNormalsHash[newMesh.vertices[i]] + newMesh.normals[i]).normalized;
		}

		//Convert to Array
		var averageNormals = new Vector3[newMesh.vertexCount];
		for (var i = 0; i < newMesh.vertexCount; i++)
		{
			averageNormals[i] = averageNormalsHash[newMesh.vertices[i]];
		}

		// Store in Vertex Colors
		if (saveChannel == SmoothedNormalsChannel.VertexColor)
		{
			var colors = new Color[newMesh.vertexCount];
			for (var i = 0; i < newMesh.vertexCount; i++)
			{
				var r = (averageNormals[i].x * 0.5f) + 0.5f;
				var g = (averageNormals[i].y * 0.5f) + 0.5f;
				var b = (averageNormals[i].z * 0.5f) + 0.5f;

				colors[i] = new Color(r, g, b, 1);
			}
			newMesh.colors = colors;
		}

		// Store in Tangents
		if (saveChannel == SmoothedNormalsChannel.Tangent)
		{
			var tangents = new Vector4[newMesh.vertexCount];
			for (var i = 0; i < newMesh.vertexCount; i++)
			{
				tangents[i] = new Vector4(averageNormals[i].x, averageNormals[i].y, averageNormals[i].z, 0f);
			}
			newMesh.tangents = tangents;
		}

		// Store in UVs
		if (saveChannel == SmoothedNormalsChannel.UV1 || 
			saveChannel == SmoothedNormalsChannel.UV2 || 
			saveChannel == SmoothedNormalsChannel.UV3 || 
			saveChannel == SmoothedNormalsChannel.UV4)
		{
			int uvIndex = -1;
			switch (saveChannel)
			{
				case SmoothedNormalsChannel.UV1: uvIndex = 1; break;
				case SmoothedNormalsChannel.UV2: uvIndex = 2; break;
				case SmoothedNormalsChannel.UV3: uvIndex = 3; break;
				case SmoothedNormalsChannel.UV4: uvIndex = 4; break;
				default: Debug.LogError("Invalid smoothed normals UV channel: " + saveChannel); break;
			}
			if (saveInTangentSpace)
			{
				var uv = new Vector2[newMesh.vertexCount];
				var tangents = newMesh.tangents;
				var normals = newMesh.normals;
				var bitangent = Vector3.one;
				for (var j = 0; j < newMesh.vertexCount; j++)
				{
                    bitangent = (Vector3.Cross(normals[j], tangents[j]) * tangents[j].w).normalized;
                    var bakeNormal = Vector3.Normalize(new Vector3(
                            Vector3.Dot(tangents[j], averageNormals[j]),
                            Vector3.Dot(bitangent, averageNormals[j]),
                            Vector3.Dot(normals[j], averageNormals[j])));
                    uv[j] = new Vector2(bakeNormal.x * 0.5f + 0.5f, bakeNormal.y * 0.5f + 0.5f);
                }
				newMesh.SetUVs(uvIndex, uv);
			}
			else 
			{
				newMesh.SetUVs(uvIndex, new List<Vector3>(averageNormals));
			}
		}

		return newMesh;
	}

	private static void CopyBlendShapes(Mesh originalMesh, Mesh newMesh)
	{
		for (int i = 0; i < originalMesh.blendShapeCount; i++)
		{
			string shapeName = originalMesh.GetBlendShapeName(i);
			int frameCount = originalMesh.GetBlendShapeFrameCount(i);
			for (var j = 0; j < frameCount; j++)
			{
				Vector3[] dv = new Vector3[originalMesh.vertexCount];
				Vector3[] dn = new Vector3[originalMesh.vertexCount];
				Vector3[] dt = new Vector3[originalMesh.vertexCount];

				float frameWeight = originalMesh.GetBlendShapeFrameWeight(i, j);
				originalMesh.GetBlendShapeFrameVertices(i, j, dv, dn, dt);
				newMesh.AddBlendShapeFrame(shapeName, frameWeight, dv, dn, dt);
			}
		}
	}
	#endregion

	public class GUIHelper 
	{
		public static GUIStyle _LineStyle;
		public static GUIStyle LineStyle
		{
			get
			{
				if (_LineStyle == null)
				{
					_LineStyle = new GUIStyle();
					_LineStyle.normal.background = EditorGUIUtility.whiteTexture;
					_LineStyle.stretchWidth = true;
				}

				return _LineStyle;
			}
		}

		public static void GUILine(float height = 2f)
		{
			GUILine(Color.black, height);
		}

		public static void GUILine(Color color, float height = 2f)
		{
			var position = GUILayoutUtility.GetRect(0f, float.MaxValue, height, height, LineStyle);

			if (Event.current.type == EventType.Repaint)
			{
				var orgColor = GUI.color;
				GUI.color = orgColor * color;
				LineStyle.Draw(position, false, false, false, false);
				GUI.color = orgColor;
			}
		}

		public static void GUILine(Rect position, Color color, float height = 2f)
		{
			if (Event.current.type == EventType.Repaint)
			{
				var orgColor = GUI.color;
				GUI.color = orgColor * color;
				LineStyle.Draw(position, false, false, false, false);
				GUI.color = orgColor;
			}
		}

		public static void SeparatorSimple()
		{
			var color = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.65f, 0.65f, 0.65f);
			GUILine(color, 1);
			GUILayout.Space(1);
		}

		public static string SelectPath(string label, string startDir)
		{
			string output = null;

			if (startDir.Length > 0 && startDir[0] != '/')
			{
				startDir = "/" + startDir;
			}

			string startPath = Application.dataPath.Replace(@"\", "/") + startDir;
			if (!Directory.Exists(startPath))
			{
				startPath = Application.dataPath;
			}

			var path = EditorUtility.OpenFolderPanel(label, startPath, "");
			if (!string.IsNullOrEmpty(path))
			{
				var validPath = SystemToUnityPath(ref path);
				if (validPath)
				{
					if (path == "Assets")
						output = "/";
					else
						output = path.Substring("Assets/".Length);
				}
				else
				{
					EditorApplication.Beep();
					EditorUtility.DisplayDialog("Invalid Path", 
						"The selected path is invalid.\n\n" +
						"Please select a folder inside the \"Assets\" folder of your project!", "Ok");
				}
			}

			return output;
		}

		public static bool SystemToUnityPath(ref string sysPath)
		{
			if (sysPath.IndexOf(Application.dataPath) < 0)
			{
				return false;
			}

			sysPath = string.Format("Assets{0}", sysPath.Replace(Application.dataPath, ""));
			return true;
		}
	}
}

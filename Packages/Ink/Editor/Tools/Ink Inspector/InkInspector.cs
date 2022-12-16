using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;
using Object = UnityEngine.Object;

namespace Ink.UnityIntegration {
	public class InkInspector : DefaultAssetInspector {

		private InkFile inkFile;
		private ReorderableList includesFileList;
		private ReorderableList parentsFileList;
		private ReorderableList mastersFileList;
		private ReorderableList errorList;
		private ReorderableList warningList;
		private ReorderableList todosList;
		private string cachedTrimmedFileContents;
		private const int maxCharacters = 16000;

		public override bool IsValid(string assetPath) {
			return Path.GetExtension(assetPath) == InkEditorUtils.inkFileExtension;
		}

		public override void OnHeaderGUI () {
			GUILayout.BeginHorizontal();
			GUILayout.Space(38f);
			GUILayout.BeginVertical();
			GUILayout.Space(19f);
			GUILayout.BeginHorizontal();

			GUILayoutUtility.GetRect(10f, 10f, 16f, 35f, EditorStyles.layerMaskField);
			GUILayout.FlexibleSpace();

			EditorGUI.BeginDisabledGroup(inkFile == null);
			if (GUILayout.Button("Open", EditorStyles.miniButton)) {
				AssetDatabase.OpenAsset(inkFile.inkAsset, 3);
				GUIUtility.ExitGUI();
			}
			EditorGUI.EndDisabledGroup();

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();

			Rect lastRect = GUILayoutUtility.GetLastRect();
			Rect rect = new Rect(lastRect.x, lastRect.y, lastRect.width, lastRect.height);
			Rect iconRect = new Rect(rect.x + 6f, rect.y + 6f, 32f, 32f);
			GUI.DrawTexture(iconRect, InkBrowserIcons.inkFileIconLarge);
			Rect childIconRect = new Rect(iconRect.x, iconRect.y, 16f, 16f);
			if(inkFile == null) {
				GUI.DrawTexture(childIconRect, InkBrowserIcons.unknownFileIcon, ScaleMode.ScaleToFit);
			} else if(!inkFile.isMaster) {
				GUI.DrawTexture(childIconRect, InkBrowserIcons.childIconLarge, ScaleMode.ScaleToFit);
			}

			Rect titleRect = new Rect(rect.x + 44f, rect.y + 6f, rect.width - 44f - 38f - 4f, 16f);
			titleRect.yMin -= 2f;
			titleRect.yMax += 2f;
			GUI.Label(titleRect, editor.target.name, EditorStyles.largeLabel);
		}

		public override void OnEnable () {
			Rebuild();
			InkCompiler.OnCompileInk += OnCompileInk;
		}

		public override void OnDisable () {
			InkCompiler.OnCompileInk -= OnCompileInk;
		}

		void OnCompileInk (InkFile[] inkFiles) {
			// We could probably be smarter about when we rebuild - only rebuilding if the file that's shown in the inspector is in the list - but it's not frequent or expensive so it's not important!
			Rebuild();
		}

		void Rebuild () {
			cachedTrimmedFileContents = "";
			string assetPath = AssetDatabase.GetAssetPath(target);
			inkFile = InkLibrary.GetInkFileWithPath(assetPath);
			if(inkFile == null) 
				return;

			if (inkFile.includes.Count > 0) CreateIncludeList ();
			else includesFileList = null;

			if (inkFile.parents.Count > 0) CreateParentsList ();
			else parentsFileList = null;
			
			if (inkFile.masterInkAssets.Count > 0) CreateMastersList ();
			else mastersFileList = null;

			CreateErrorList();
			CreateWarningList();
			CreateTodoList();
			cachedTrimmedFileContents = inkFile.GetFileContents();
			cachedTrimmedFileContents = cachedTrimmedFileContents.Substring(0, Mathf.Min(cachedTrimmedFileContents.Length, maxCharacters));
			if(cachedTrimmedFileContents.Length >= maxCharacters)
				cachedTrimmedFileContents += "...\n\n<...etc...>";
		}

		void CreateIncludeList () {
			List<DefaultAsset> includeTextAssets = inkFile.includes;
			includesFileList = new ReorderableList(includeTextAssets, typeof(DefaultAsset), false, true, false, false);
			includesFileList.drawHeaderCallback = (Rect rect) => {  
				EditorGUI.LabelField(rect, "Included Files");
			};
			includesFileList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
				DefaultAsset childAssetFile = ((List<DefaultAsset>)includesFileList.list)[index];
				if(childAssetFile == null) {
					Debug.LogError("Ink file in include list is null. This should never occur. Use Assets > Recompile Ink to fix this issue.");
					EditorGUI.LabelField(rect, new GUIContent("Warning: Ink File in include list is null. Use Assets > Recompile Ink to fix this issue."));
					return;
				}
				InkFile childInkFile = InkLibrary.GetInkFileWithFile(childAssetFile);
				if(childInkFile == null) {
					Debug.LogError("Ink File for included file "+childAssetFile+" not found. This should never occur. Use Assets > Recompile Ink to fix this issue.");
					EditorGUI.LabelField(rect, new GUIContent("Warning: Ink File for included file "+childAssetFile+" not found. Use Assets > Recompile Ink to fix this issue."));
					return;
				}
				Rect iconRect = new Rect(rect.x, rect.y, 0, 16);
				if(childInkFile.hasErrors || childInkFile.hasWarnings) {
					iconRect.width = 20;
				}
				Rect objectFieldRect = new Rect(iconRect.xMax, rect.y, rect.width - iconRect.width - 80, 16);
				Rect selectRect = new Rect(objectFieldRect.xMax, rect.y, 80, 16);
				if(childInkFile.hasErrors) {
					EditorGUI.LabelField(iconRect, new GUIContent(InkBrowserIcons.errorIcon));
				} else if(childInkFile.hasWarnings) {
					EditorGUI.LabelField(iconRect, new GUIContent(InkBrowserIcons.warningIcon));
				}
				EditorGUI.BeginDisabledGroup(true);
				EditorGUI.ObjectField(objectFieldRect, childAssetFile, typeof(Object), false);
				EditorGUI.EndDisabledGroup();
				if(GUI.Button(selectRect, "Select")) {
					Selection.activeObject = childAssetFile;
				}
			};
		}

		void CreateParentsList () {
			List<DefaultAsset> parentsTextAssets = inkFile.parents;
			parentsFileList = new ReorderableList(parentsTextAssets, typeof(DefaultAsset), false, true, false, false);
			parentsFileList.drawHeaderCallback = (Rect rect) => {  
				EditorGUI.LabelField(rect, "Parent Files");
			};
			parentsFileList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
				DefaultAsset parentAssetFile = ((List<DefaultAsset>)parentsFileList.list)[index];
				if(parentAssetFile == null) {
					Debug.LogError("Ink file in parents list is null. This should never occur. Use Assets > Recompile Ink to fix this issue.");
					EditorGUI.LabelField(rect, new GUIContent("Warning: Ink File in parents list is null. Use Assets > Recompile Ink to fix this issue."));
					return;
				}
				InkFile parentInkFile = InkLibrary.GetInkFileWithFile(parentAssetFile);
				if(parentInkFile == null) {
					Debug.LogError("Ink File for parent file "+parentAssetFile+" not found. This should never occur. Use Assets > Recompile Ink to fix this issue.");
					EditorGUI.LabelField(rect, new GUIContent("Warning: Ink File for parent file "+parentAssetFile+" not found. Use Assets > Recompile Ink to fix this issue."));
					return;
				}
				Rect iconRect = new Rect(rect.x, rect.y, 0, 16);
				if(parentInkFile.hasErrors || parentInkFile.hasWarnings) {
					iconRect.width = 20;
				}
				Rect objectFieldRect = new Rect(iconRect.xMax, rect.y, rect.width - iconRect.width - 80, 16);
				Rect selectRect = new Rect(objectFieldRect.xMax, rect.y, 80, 16);
				if(parentInkFile.hasErrors) {
					EditorGUI.LabelField(iconRect, new GUIContent(InkBrowserIcons.errorIcon));
				} else if(parentInkFile.hasWarnings) {
					EditorGUI.LabelField(iconRect, new GUIContent(InkBrowserIcons.warningIcon));
				}
				EditorGUI.BeginDisabledGroup(true);
				EditorGUI.ObjectField(objectFieldRect, parentAssetFile, typeof(Object), false);
				EditorGUI.EndDisabledGroup();
				if(GUI.Button(selectRect, "Select")) {
					Selection.activeObject = parentAssetFile;
				}
			};
		}
		
		void CreateMastersList () {
			List<DefaultAsset> mastersTextAssets = inkFile.masterInkAssets;
			mastersFileList = new ReorderableList(mastersTextAssets, typeof(DefaultAsset), false, true, false, false);
			mastersFileList.drawHeaderCallback = (Rect rect) => {  
				EditorGUI.LabelField(rect, "Master Files");
			};
			mastersFileList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
				DefaultAsset masterAssetFile = ((List<DefaultAsset>)mastersFileList.list)[index];
				if(masterAssetFile == null) {
					Debug.LogError("Ink file in masters list is null. This should never occur. Use Assets > Recompile Ink to fix this issue.");
					EditorGUI.LabelField(rect, new GUIContent("Warning: Ink File in masters list is null. Use Assets > Recompile Ink to fix this issue."));
					return;
				}
				InkFile masterInkFile = InkLibrary.GetInkFileWithFile(masterAssetFile);
				if(masterInkFile == null) {
					Debug.LogError("Ink File for master file "+masterAssetFile+" not found. This should never occur. Use Assets > Recompile Ink to fix this issue.");
					EditorGUI.LabelField(rect, new GUIContent("Warning: Ink File for master file "+masterAssetFile+" not found. Use Assets > Recompile Ink to fix this issue."));
					return;
				}
				Rect iconRect = new Rect(rect.x, rect.y, 0, 16);
				if(masterInkFile.hasErrors || masterInkFile.hasWarnings) {
					iconRect.width = 20;
				}
				Rect objectFieldRect = new Rect(iconRect.xMax, rect.y, rect.width - iconRect.width - 80, 16);
				Rect selectRect = new Rect(objectFieldRect.xMax, rect.y, 80, 16);
				if(masterInkFile.hasErrors) {
					EditorGUI.LabelField(iconRect, new GUIContent(InkBrowserIcons.errorIcon));
				} else if(masterInkFile.hasWarnings) {
					EditorGUI.LabelField(iconRect, new GUIContent(InkBrowserIcons.warningIcon));
				}
				EditorGUI.BeginDisabledGroup(true);
				EditorGUI.ObjectField(objectFieldRect, masterAssetFile, typeof(Object), false);
				EditorGUI.EndDisabledGroup();
				if(GUI.Button(selectRect, "Select")) {
					Selection.activeObject = masterAssetFile;
				}

				
			// foreach(var masterInkFile in inkFile.masterInkFiles) {
			// 	EditorGUILayout.BeginHorizontal();
			// 	if(masterInkFile.hasErrors) {
			// 		GUILayout.Label(new GUIContent(InkBrowserIcons.errorIcon), GUILayout.Width(20));
			// 	} else if(masterInkFile.hasWarnings) {
			// 		GUILayout.Label(new GUIContent(InkBrowserIcons.warningIcon), GUILayout.Width(20));
			// 	}
			// 	EditorGUI.BeginDisabledGroup(true);
			// 	EditorGUILayout.ObjectField("Master Ink File", masterInkFile.inkAsset, typeof(Object), false);
			// 	EditorGUI.EndDisabledGroup();
			// 	if(GUILayout.Button("Select", GUILayout.Width(80))) {
			// 		Selection.activeObject = masterInkFile.inkAsset;
			// 	}
			// 	EditorGUILayout.EndHorizontal();
			// }
			};
		}

		void CreateErrorList () {
			errorList = new ReorderableList(inkFile.errors, typeof(string), false, true, false, false);
			errorList.drawHeaderCallback = (Rect rect) => {  
				EditorGUI.LabelField(rect, new GUIContent(InkBrowserIcons.errorIcon), new GUIContent("Errors"));
			};
			errorList.elementHeight = 18;
			errorList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
				Rect labelRect = new Rect(rect.x, rect.y, rect.width - 80, rect.height);
				Rect buttonRect = new Rect(labelRect.xMax, rect.y, 80, rect.height-2);
				InkCompilerLog log = ((List<InkCompilerLog>)errorList.list)[index];
				string label = log.content;
				GUI.Label(labelRect, label);
				string openLabel = "Open"+ (log.lineNumber == -1 ? "" : " ("+log.lineNumber+")");
				if(GUI.Button(buttonRect, openLabel)) {
					InkEditorUtils.OpenInEditor(inkFile, log);
				}
			};
		}

		void CreateWarningList () {
			warningList = new ReorderableList(inkFile.warnings, typeof(string), false, true, false, false);
			warningList.elementHeight = 18;
			warningList.drawHeaderCallback = (Rect rect) => {  
				EditorGUI.LabelField(rect, new GUIContent(InkBrowserIcons.warningIcon), new GUIContent("Warnings"));
			};
			warningList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
				Rect labelRect = new Rect(rect.x, rect.y, rect.width - 80, rect.height);
				Rect buttonRect = new Rect(labelRect.xMax, rect.y, 80, rect.height-2);
				InkCompilerLog log = ((List<InkCompilerLog>)warningList.list)[index];
				string label = log.content;
				GUI.Label(labelRect, label);
				string openLabel = "Open"+ (log.lineNumber == -1 ? "" : " ("+log.lineNumber+")");
				if(GUI.Button(buttonRect, openLabel)) {
					InkEditorUtils.OpenInEditor(inkFile, log);
				}
			};
		}

		void CreateTodoList () {
			todosList = new ReorderableList(inkFile.todos, typeof(string), false, true, false, false);
			todosList.elementHeight = 18;
			todosList.drawHeaderCallback = (Rect rect) => {  
				EditorGUI.LabelField(rect, "To do");
			};
			todosList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
				Rect labelRect = new Rect(rect.x, rect.y, rect.width - 80, rect.height);
				Rect buttonRect = new Rect(labelRect.xMax, rect.y, 80, rect.height-2);
				InkCompilerLog log = ((List<InkCompilerLog>)todosList.list)[index];
				string label = log.content;
				GUI.Label(labelRect, label);
				string openLabel = "Open"+ (log.lineNumber == -1 ? "" : " ("+log.lineNumber+")");
				if(GUI.Button(buttonRect, openLabel)) {
					InkEditorUtils.OpenInEditor(inkFile, log);
				}
			};
		}

		public static void DrawLayoutInkLine (InkFile inkFile, int lineNumber, string label) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);
			string openLabel = "Open"+ (lineNumber == -1 ? "" : " ("+lineNumber+")");
			if(GUILayout.Button(openLabel, GUILayout.Width(80))) {
				InkEditorUtils.OpenInEditor(inkFile.filePath, null, lineNumber);
			}
			GUILayout.EndHorizontal();
		}

		public override void OnInspectorGUI () {
			editor.Repaint();
			serializedObject.Update();
			if(inkFile == null) {
				EditorGUILayout.HelpBox("Ink File is not in library.", MessageType.Warning);
				if(GUILayout.Button("Rebuild Library")) {
					InkLibrary.Rebuild();
					Rebuild();
				}
				return;
			}

			if(GUILayout.Button("TEst")) {
				// InkCompiler.CompileInk(inkFile.masterInkFilesIncludingSelf.ToArray());
				InkLibrary.RebuildInkFileConnections();
			}
			
			if(InkCompiler.IsInkFileOnCompilationStack(inkFile)) {
				EditorGUILayout.HelpBox("File is compiling...", MessageType.Info);
				return;
			}
			
			if(!inkFile.isMaster) {
				EditorGUI.BeginChangeCheck();
				var newCompileAsIfMaster = EditorGUILayout.Toggle(new GUIContent("Compile As If Master File", "This file is included by another ink file. Typically, these files don't want to be compiled, but this option enables them to be for special purposes."), InkSettings.instance.includeFilesToCompileAsMasterFiles.Contains(inkFile.inkAsset));
				if(EditorGUI.EndChangeCheck()) {
					if(newCompileAsIfMaster) {
						InkSettings.instance.includeFilesToCompileAsMasterFiles.Add(inkFile.inkAsset);
						EditorUtility.SetDirty(InkSettings.instance);
					} else {
						InkSettings.instance.includeFilesToCompileAsMasterFiles.Remove(inkFile.inkAsset);
						EditorUtility.SetDirty(InkSettings.instance);
					}
				}
				EditorApplication.RepaintProjectWindow();
			}

			if(inkFile.compileAsMasterFile) {
				DrawMasterFileHeader();
				DrawEditAndCompileDates(inkFile);
				if(inkFile.hasUnhandledCompileErrors) {
					EditorGUILayout.HelpBox("Last compiled failed", MessageType.Error);
				} if(inkFile.hasErrors) {
					EditorGUILayout.HelpBox("Last compiled had errors", MessageType.Error);
				} else if(inkFile.hasWarnings) {
					EditorGUILayout.HelpBox("Last compile had warnings", MessageType.Warning);
				} else if(inkFile.jsonAsset == null) {
					EditorGUILayout.HelpBox("Ink file has not been compiled", MessageType.Warning);
				}
				if(inkFile.requiresCompile && GUILayout.Button("Compile")) {
					InkCompiler.CompileInk(inkFile);
				}

				DrawCompileErrors();
				DrawErrors();
				DrawWarnings();
				DrawTODOList();
			} else {
				EditorGUILayout.LabelField("Include File", EditorStyles.boldLabel);
			}

			DrawListOfParentFiles();
			DrawListOfMasterFiles();
			DrawIncludedFiles();
			DrawFileContents ();
			

			serializedObject.ApplyModifiedProperties();
		}

		void DrawMasterFileHeader () {
			if(inkFile.isMaster) EditorGUILayout.LabelField("Master File", EditorStyles.boldLabel);
			if(!inkFile.isMaster) EditorGUILayout.LabelField("Include treated as Master", EditorStyles.boldLabel);
			if(!InkSettings.instance.compileAllFilesAutomatically) {
				EditorGUI.BeginChangeCheck();
				var newCompileAutomatically = EditorGUILayout.Toggle(new GUIContent("Compile Automatially", "If true, this file recompiles automatically when any changes are detected."), InkSettings.instance.ShouldCompileInkFileAutomatically(inkFile));
				if(EditorGUI.EndChangeCheck()) {
					if(newCompileAutomatically) {
						InkSettings.instance.filesToCompileAutomatically.Add(inkFile.inkAsset);
						EditorUtility.SetDirty(InkSettings.instance);
					} else {
						InkSettings.instance.filesToCompileAutomatically.Remove(inkFile.inkAsset);
						EditorUtility.SetDirty(InkSettings.instance);
					}
				}
				EditorApplication.RepaintProjectWindow();
			}
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.ObjectField("JSON Asset", inkFile.jsonAsset, typeof(TextAsset), false);
			EditorGUI.EndDisabledGroup();

			if(inkFile.jsonAsset != null && inkFile.errors.Count == 0 && GUILayout.Button("Play")) {
				InkPlayerWindow.LoadAndPlay(inkFile.jsonAsset);
			}

//				if(!checkedStoryForErrors) {
//					if(GUILayout.Button("Check for errors")) {
//						GetStoryErrors();
//					}
//				} else {
//					if(exception != null) {
//						EditorGUILayout.HelpBox("Story is invalid\n"+exception.ToString(), MessageType.Error);
//					} else {
//						EditorGUILayout.HelpBox("Story is valid", MessageType.Info);
//					}
//				}
		}

		void DrawListOfParentFiles() {
			if(parentsFileList != null && parentsFileList.count > 0) {
				parentsFileList.DoLayoutList();
			}
		}
		
		void DrawListOfMasterFiles() {
			if(mastersFileList != null && mastersFileList.count > 0) {
				mastersFileList.DoLayoutList();
			}
		}

		void DrawEditAndCompileDates (InkFile masterInkFile) {
			string editAndCompileDateString = "";
			DateTime lastEditDate = inkFile.lastEditDate;
			editAndCompileDateString += "Last edit date "+lastEditDate.ToString();
			if(masterInkFile.jsonAsset != null) {
				DateTime lastCompileDate = masterInkFile.lastCompileDate;
				editAndCompileDateString += "\nLast compile date "+lastCompileDate.ToString();
				if(lastEditDate > lastCompileDate) {
                    if(EditorApplication.isPlaying && InkSettings.instance.delayInPlayMode) {
					    editAndCompileDateString += "\nWill compile on exiting play mode";
                        EditorGUILayout.HelpBox(editAndCompileDateString, MessageType.Info);
                    } else {
					    EditorGUILayout.HelpBox(editAndCompileDateString, MessageType.Warning);
                    }
				} else {
					EditorGUILayout.HelpBox(editAndCompileDateString, MessageType.None);
				}
			} else {
				EditorGUILayout.HelpBox(editAndCompileDateString, MessageType.None);
			}
		}

		void DrawIncludedFiles () {
			if(includesFileList != null && includesFileList.count > 0) {
				includesFileList.DoLayoutList();
			}
		}

		void DrawCompileErrors () {
			if(inkFile.unhandledCompileErrors.Count == 0) 
				return;
			EditorGUILayout.BeginVertical(GUI.skin.box);
			EditorGUILayout.HelpBox("Compiler bug prevented compilation of JSON file. Please help us fix it by reporting this as a bug.", MessageType.Error);
			EditorGUILayout.BeginHorizontal();
			if(GUILayout.Button("Report via Github")) {
				Application.OpenURL("https://github.com/inkle/ink-unity-integration/issues/new");
			}
			if(GUILayout.Button("Report via Email")) {
				Application.OpenURL("mailto:info@inklestudios.com");
			}
			EditorGUILayout.EndHorizontal();
			foreach(string compileError in inkFile.unhandledCompileErrors) {
				GUILayout.TextArea(compileError);
			}
			EditorGUILayout.EndVertical();
		}

		void DrawErrors () {
			if(errorList != null && errorList.count > 0) {
				errorList.DoLayoutList();
			}
		}

		void DrawWarnings () {
			if(warningList != null && warningList.count > 0) {
				warningList.DoLayoutList();
			}
		}

		void DrawTODOList () {
			if(todosList != null && todosList.count > 0) {
				todosList.DoLayoutList();
			}
		}

		void DrawFileContents () {
			float width = EditorGUIUtility.currentViewWidth-50;
			float height = EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(cachedTrimmedFileContents), width);
			EditorGUILayout.BeginVertical(EditorStyles.textArea);
			EditorGUILayout.SelectableLabel(cachedTrimmedFileContents, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true), GUILayout.Width(width), GUILayout.Height(height));
			EditorGUILayout.EndVertical();
		}
	}
}
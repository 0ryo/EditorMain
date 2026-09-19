using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public static class UnityPdfAudit
{
    public static string InspectFields() {
        var v=UnityEngine.Object.FindFirstObjectByType<SkillSyncDesignView>();
        return string.Join("\n",v.visuals.Where(x=>x.label!=null && (x.label.text=="cm"||x.label.text=="°")).Select(x=>$"{x.label.text} mask={x.mask} active={x.target.activeInHierarchy} pos={x.label.rectTransform.anchoredPosition} color={x.label.color} bounds={x.label.textBounds} cull={x.label.canvasRenderer.cull} chars={x.label.textInfo.characterCount} overflow={x.label.overflowMode}"));
    }
    public static async Task<string> VerifySixStates()
    {
        var fixture=UnityEngine.Object.FindFirstObjectByType<SkillSyncReferencePreview>();
        if(!Application.isPlaying || fixture==null) throw new InvalidOperationException("Play isolated reference scene");
        Application.runInBackground=true;
        var view=fixture.view;var lines=new List<string>();
        for(int state=0;state<6;state++) {
            fixture.ApplyState(state);await Task.Delay(100);Canvas.ForceUpdateCanvases();
            int count=0;
            foreach(var c in view.controls.Where(c=>c.button.gameObject.activeInHierarchy)) {
                var rect=(RectTransform)c.button.transform;
                foreach(var label in c.button.GetComponentsInChildren<TMP_Text>().Where(t=>!string.IsNullOrEmpty(t.text))) {
                    label.ForceMeshUpdate();
                    if(label.alignment!=TextAlignmentOptions.Midline) throw new InvalidOperationException("Not centered: "+c.action);
                    var difference=(Vector2)rect.InverseTransformPoint(label.rectTransform.TransformPoint(label.rectTransform.rect.center))-rect.rect.center;
                    if(difference.magnitude>.01f) throw new InvalidOperationException("Caption rectangle differs from button: "+c.action);
                    var ink=(Vector2)rect.InverseTransformPoint(label.rectTransform.TransformPoint(label.textBounds.center))-rect.rect.center;
                    if(Mathf.Abs(ink.y)>.35f || Mathf.Abs(ink.x)>.35f) throw new InvalidOperationException($"Caption ink not centered within half a rendered pixel: {c.action}, offset={ink}");
                    if(label.isTextOverflowing) throw new InvalidOperationException("Caption overflow: "+c.action);
                    count++;
                }
            }
            if(view.controls.Any(c=>c.action=="選択を表示" && c.button.gameObject.activeInHierarchy)) throw new InvalidOperationException("Removed control still visible");
            if(view.visuals.Any(v=>v.label!=null && v.label.text=="上" && v.target.activeInHierarchy)) throw new InvalidOperationException("Gizmo regression");
            foreach(var field in view.fields.Where(f=>f.input.gameObject.activeInHierarchy && new[]{"height","angle","distance","hold"}.Contains(f.role))) {
                string unit=field.role=="angle"?"°":field.role=="hold"?"秒":"cm";
                if(!field.input.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==unit)) throw new InvalidOperationException("Missing input unit: "+field.role);
            }
            lines.Add($"State {state}: {count} visible captions share the button rectangle, use geometry-centered alignment, no text overflow.");
        }
        File.WriteAllText("Design/skillsync_codex_handoff/verification/pdf-alignment-results.txt",string.Join("\n",lines));return string.Join("\n",lines);
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumGraphService : MonoBehaviour
{
    public Curriculum curriculum = new Curriculum();

    int stepSequence = 0;

    public StepNode AddStep()
    {
        var step = new StepNode
        {
            id = "step-" + (++stepSequence).ToString("D4"),
            title = "手順 " + stepSequence
        };

        step.conditions.Add(new ProximityPair { aObjectId = null, bObjectId = null });
        curriculum.steps.Add(step);
        return step;
    }

    public void RemoveStep(string stepId)
    {
        curriculum.steps.RemoveAll(s => s.id == stepId);

        foreach (var step in curriculum.steps)
        {
            step.nextStepIds.RemoveAll(n => n == stepId);
        }
    }

    public StepNode FindStep(string stepId)
    {
        return curriculum.steps.FirstOrDefault(s => s.id == stepId);
    }

    public void AddEdge(string fromStepId, string toStepId)
    {
        if (fromStepId == toStepId) return;

        var from = FindStep(fromStepId);
        var to = FindStep(toStepId);
        if (from == null || to == null) return;
        if (from.nextStepIds.Contains(toStepId)) return;

        from.nextStepIds.Add(toStepId);
    }

    public void RemoveEdge(string fromStepId, string toStepId)
    {
        var from = FindStep(fromStepId);
        if (from == null) return;

        from.nextStepIds.RemoveAll(n => n == toStepId);
    }

    public List<string> GetParents(string stepId)
    {
        var parents = new List<string>();

        foreach (var step in curriculum.steps)
        {
            if (step.nextStepIds.Contains(stepId))
            {
                parents.Add(step.id);
            }
        }

        return parents;
    }

    public bool RepairBrokenReferences()
    {
        var placedObjectIds = FindObjectsOfType<PlacedObject>().Select(p => p.id).ToHashSet();
        bool changed = false;

        foreach (var step in curriculum.steps)
        {
            foreach (var condition in step.conditions)
            {
                if (!string.IsNullOrEmpty(condition.aObjectId) && !placedObjectIds.Contains(condition.aObjectId))
                {
                    condition.aObjectId = null;
                    changed = true;
                }

                if (!string.IsNullOrEmpty(condition.bObjectId) && !placedObjectIds.Contains(condition.bObjectId))
                {
                    condition.bObjectId = null;
                    changed = true;
                }
            }
        }

        return changed;
    }

    public bool HasUnconfiguredConditions(StepNode step)
    {
        return step.conditions.Any(c => string.IsNullOrEmpty(c.aObjectId) || string.IsNullOrEmpty(c.bObjectId));
    }
}

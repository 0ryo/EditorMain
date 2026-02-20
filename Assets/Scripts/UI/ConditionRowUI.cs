using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConditionRowUI : MonoBehaviour
{
    public Dropdown dropdownA;
    public Dropdown dropdownB;
    public Text textAfterA;
    public Text textAfterB;

    public void Bind(
        List<PlacedObjectOptionProvider.Option> options,
        string currentAId,
        string currentBId,
        Action<string> onAChanged,
        Action<string> onBChanged
    )
    {
        var labels = new List<string> { "未設定" };
        foreach (var option in options)
        {
            labels.Add(option.label);
        }

        dropdownA.ClearOptions();
        dropdownB.ClearOptions();
        dropdownA.AddOptions(labels);
        dropdownB.AddOptions(labels);

        dropdownA.onValueChanged.RemoveAllListeners();
        dropdownB.onValueChanged.RemoveAllListeners();

        dropdownA.value = IdToIndex(options, currentAId);
        dropdownB.value = IdToIndex(options, currentBId);

        dropdownA.onValueChanged.AddListener(v => onAChanged?.Invoke(IndexToId(options, v)));
        dropdownB.onValueChanged.AddListener(v => onBChanged?.Invoke(IndexToId(options, v)));

        if (textAfterA != null) textAfterA.text = "を";
        if (textAfterB != null) textAfterB.text = "に近づけたら";
    }

    int IdToIndex(List<PlacedObjectOptionProvider.Option> options, string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].id == id) return i + 1;
        }

        return 0;
    }

    string IndexToId(List<PlacedObjectOptionProvider.Option> options, int index)
    {
        if (index <= 0) return null;

        int optionIndex = index - 1;
        if (optionIndex < 0 || optionIndex >= options.Count) return null;

        return options[optionIndex].id;
    }
}

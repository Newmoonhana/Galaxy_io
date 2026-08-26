using ImpossibleRobert.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Automator
{
    /// <summary>
    /// Editor window for creating and editing automation actions.
    /// </summary>
    public class ActionEditorWindow : CommonEditorUI
    {
        private IActionRepository _repository;
        private List<ActionStepDefinition> _steps = new List<ActionStepDefinition>();
        private Action _onSave;
        private Dictionary<string, List<Tuple<string, ParameterValue>>> _parameterOptionsCache = new Dictionary<string, List<Tuple<string, ParameterValue>>>();
        private ListView _stepsList;
        private VisualElement _editorRoot;
        private Label _stepsSummary;

        private ActionDefinition _action;

        public static ActionEditorWindow ShowWindow()
        {
            ActionEditorWindow window = GetWindow<ActionEditorWindow>("Action Editor");
            window.minSize = new Vector2(690, 300);
            return window;
        }

        public static ActionEditorWindow CreateNew(IActionRepository repository, Action onSave = null)
        {
            ActionEditorWindow window = ShowWindow();

            ActionDefinition newAction = new ActionDefinition("New Action");
            newAction = repository.SaveAction(newAction);
            repository.Save();

            window.Init(repository, newAction, onSave);
            return window;
        }

        public static ActionEditorWindow Edit(IActionRepository repository, ActionDefinition action, Action onSave = null)
        {
            ActionEditorWindow window = ShowWindow();
            window.Init(repository, action, onSave);
            return window;
        }

        public void Init(IActionRepository repository, ActionDefinition action, Action onSave = null)
        {
            _repository = repository;
            _action = action;
            _onSave = onSave;

            _steps = _repository.GetSteps(_action.Id);
            _parameterOptionsCache.Clear();
            RebuildNativeEditor();
        }

        public void CreateGUI()
        {
            RebuildNativeEditor();
        }

        private void RebuildNativeEditor()
        {
            if (rootVisualElement == null)
                return;

            rootVisualElement.Clear();
            StyleSheet styleSheet = CommonUITK.LoadStyleSheetFromAnchor(
                "ActionEditorWindow",
                "Editor/Scripts/UI/ActionEditorWindow.cs",
                "Editor/Scripts/UI/AutomatorInspector.uss");
            _editorRoot = CommonInspectorElements.CreateRoot(
                "automator-action-editor",
                styleSheet,
                "automator-inspector",
                "automator-action-editor");
            rootVisualElement.Add(_editorRoot);

            if (_action == null || _repository == null)
            {
                _editorRoot.Add(CommonInspectorElements.CreateHelpBox(
                    "No action is loaded. Create a new action or open one from the Action Manager.",
                    HelpBoxMessageType.Warning));
                return;
            }

            ScrollView content = new ScrollView(ScrollViewMode.Vertical);
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            _editorRoot.Add(content);

            VisualElement identity = new VisualElement();
            TextField name = new TextField("Name") { value = _action.Name ?? string.Empty };
            name.RegisterValueChangedCallback(evt => _action.Name = evt.newValue);
            identity.Add(name);
            TextField description = new TextField("Description")
            {
                value = _action.Description ?? string.Empty,
                multiline = true
            };
            description.RegisterValueChangedCallback(evt => _action.Description = evt.newValue);
            identity.Add(description);
            Toggle stop = new Toggle("Stop on Failure")
            {
                value = _action.StopOnFailure,
                tooltip = "Stop remaining steps after the first failure. Disable to log failures and continue."
            };
            stop.RegisterValueChangedCallback(evt => _action.StopOnFailure = evt.newValue);
            identity.Add(stop);
            EnumField mode = new EnumField("Run Mode", _action.Mode);
            mode.RegisterValueChangedCallback(evt => _action.Mode = (ActionDefinition.RunMode)evt.newValue);
            identity.Add(mode);
            content.Add(CommonInspectorElements.CreateSection(
                "Action",
                "automator-action-identity",
                identity));

            VisualElement stepHeader = new VisualElement();
            stepHeader.AddToClassList("automator-step-header");
            _stepsSummary = CommonInspectorElements.CreateMutedText(GetStepSummary());
            stepHeader.Add(_stepsSummary);
            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            stepHeader.Add(spacer);
            ToolbarMenu addMenu = new ToolbarMenu
            {
                text = "Add Step",
                tooltip = "Add an automation step after the current selection"
            };
            foreach (ActionStep step in ActionStepRegistry.Steps)
            {
                ActionStep captured = step;
                string category = StringUtils.CamelCaseToWords(step.Category.ToString());
                addMenu.menu.AppendAction(
                    category + "/" + step.Name,
                    _ => AddStep(captured));
            }
            stepHeader.Add(addMenu);

            _stepsList = new ListView
            {
                name = "automator-step-list",
                itemsSource = _steps,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                makeItem = MakeStepElement,
                bindItem = BindStepElement,
                showBorder = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            _stepsList.AddToClassList("automator-step-list");

            VisualElement stepsBody = new VisualElement();
            stepsBody.Add(stepHeader);
            stepsBody.Add(_stepsList);
            content.Add(CommonInspectorElements.CreateSection(
                "Steps",
                "automator-action-steps",
                stepsBody));

            Button saveClose = CommonInspectorElements.CreateButton(
                "Save & Close",
                "Save this action and return to the manager",
                () =>
                {
                    Save();
                    Close();
                },
                true);
            Button saveRun = CommonInspectorElements.CreateButton(
                "Save & Run",
                "Save and execute the complete action now",
                () =>
                {
                    Save();
                    RunAction();
                });
            VisualElement footer = CommonUITK.CreateWindowFooter(10f, 10f, "automator-action-editor-footer");
            footer.Add(saveClose);
            footer.Add(saveRun);
            _editorRoot.Add(footer);
        }

        private VisualElement MakeStepElement()
        {
            VisualElement item = new VisualElement();
            item.AddToClassList("automator-step-card");
            return item;
        }

        private void BindStepElement(VisualElement item, int index)
        {
            item.Clear();
            if (index < 0 || index >= _steps.Count)
                return;

            ActionStepDefinition stepDef = _steps[index];
            ActionStep step = ActionStepRegistry.GetStep(stepDef.Key);
            if (step == null)
            {
                item.Add(CommonInspectorElements.CreateHelpBox(
                    $"Step '{stepDef.Key}' is not installed. Remove it or install the required extension.",
                    HelpBoxMessageType.Error));
                return;
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("automator-step-card__header");
            VisualElement titleBlock = new VisualElement();
            titleBlock.style.flexGrow = 1f;
            titleBlock.Add(CommonUITK.CreateLabel($"{index + 1}. {step.Name}", "automator-step-card__title"));
            titleBlock.Add(CommonInspectorElements.CreateMutedText(step.Description));
            header.Add(titleBlock);
            header.Add(CommonInspectorElements.CreateButton(
                "Run",
                "Run only this step with variables produced by earlier steps",
                () => RunStep(index)));
            header.Add(CommonInspectorElements.CreateButton(
                "Remove",
                "Remove this step",
                () => RemoveStep(index),
                false,
                true));
            item.Add(header);

            Dictionary<string, string> availableVariables = BuildAvailableVariables(index);
            while (stepDef.Values.Count < step.Parameters.Count)
            {
                int paramIndex = stepDef.Values.Count;
                stepDef.Values.Add(new ParameterValue(step.Parameters[paramIndex].DefaultValue));
            }

            for (int i = 0; i < step.Parameters.Count; i++)
            {
                int parameterIndex = i;
                StepParameter param = step.Parameters[i];
                if (!step.GetParamVisibility(param, stepDef.Values))
                    continue;

                StepParameter.ParamType finalType = param.Type;
                if (finalType == StepParameter.ParamType.Dynamic)
                    finalType = step.GetParamType(param, stepDef.Values);

                List<Tuple<string, ParameterValue>> finalOptions = param.Options;
                if (param.LazyLoadOptions)
                {
                    string cacheKey = $"{step.Key}_{param.Name}";
                    if (!_parameterOptionsCache.TryGetValue(cacheKey, out finalOptions))
                    {
                        finalOptions = step.GetParamOptions(param, stepDef.Values);
                        _parameterOptionsCache[cacheKey] = finalOptions;
                    }
                }
                else if (param.Type == StepParameter.ParamType.Dynamic)
                {
                    finalOptions = step.GetParamOptions(param, stepDef.Values);
                }

                string label = param.Name + (param.Optional ? " (optional)" : string.Empty);
                VisualElement field = CreateParameterField(
                    label,
                    param.Description,
                    finalType,
                    finalOptions,
                    stepDef.Values[i],
                    () =>
                    {
                        if (param.Type == StepParameter.ParamType.Dynamic)
                            _stepsList?.RefreshItem(index);
                    });
                if (field != null)
                    item.Add(field);

                if (finalOptions != null && finalOptions.Count > 0)
                    continue;

                if (finalType == StepParameter.ParamType.String || finalType == StepParameter.ParamType.MultilineString)
                {
                    string parameterValue = stepDef.Values[parameterIndex].stringValue;
                    List<string> undefined = string.IsNullOrEmpty(parameterValue)
                        ? new List<string>()
                        : VariableResolver.ValidateVariables(parameterValue, availableVariables);
                    if (undefined.Count > 0)
                    {
                        item.Add(CommonInspectorElements.CreateHelpBox(
                            "Undefined variables: " + string.Join(", ", undefined),
                            HelpBoxMessageType.Warning));
                    }
                }
            }
        }

        private VisualElement CreateParameterField(
            string label,
            string tooltip,
            StepParameter.ParamType type,
            List<Tuple<string, ParameterValue>> options,
            ParameterValue value,
            Action changed)
        {
            if (options != null && options.Count > 0)
            {
                List<string> choices = options.Select(option => option.Item1.Replace("/", "\\")).ToList();
                int current = type == StepParameter.ParamType.Int
                    ? options.FindIndex(option => option.Item2.intValue == value.intValue)
                    : options.FindIndex(option => option.Item2.stringValue == value.stringValue);
                current = Mathf.Clamp(current, 0, choices.Count - 1);
                if (type == StepParameter.ParamType.Int)
                    value.intValue = options[current].Item2.intValue;
                else
                    value.stringValue = options[current].Item2.stringValue;
                PopupField<string> popup = new PopupField<string>(label, choices, current)
                {
                    tooltip = tooltip
                };
                popup.RegisterValueChangedCallback(evt =>
                {
                    int index = choices.IndexOf(evt.newValue);
                    if (index < 0)
                        return;
                    if (type == StepParameter.ParamType.Int)
                        value.intValue = options[index].Item2.intValue;
                    else
                        value.stringValue = options[index].Item2.stringValue;
                    changed();
                });
                return popup;
            }

            switch (type)
            {
                case StepParameter.ParamType.String:
                case StepParameter.ParamType.MultilineString:
                    TextField text = new TextField(label)
                    {
                        value = value.stringValue ?? string.Empty,
                        multiline = type == StepParameter.ParamType.MultilineString,
                        tooltip = tooltip
                    };
                    text.RegisterValueChangedCallback(evt =>
                    {
                        value.stringValue = evt.newValue;
                        changed();
                    });
                    return text;
                case StepParameter.ParamType.Int:
                    IntegerField integer = new IntegerField(label) { value = value.intValue, tooltip = tooltip };
                    integer.RegisterValueChangedCallback(evt =>
                    {
                        value.intValue = evt.newValue;
                        changed();
                    });
                    return integer;
                case StepParameter.ParamType.Bool:
                    Toggle toggle = new Toggle(label) { value = value.boolValue, tooltip = tooltip };
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        value.boolValue = evt.newValue;
                        changed();
                    });
                    return toggle;
                default:
                    return CommonInspectorElements.CreateHelpBox(
                        $"Parameter '{label}' uses an unsupported editor type ({type}).",
                        HelpBoxMessageType.Warning);
            }
        }

        private void RemoveStep(int index)
        {
            if (index < 0 || index >= _steps.Count)
                return;
            _steps.RemoveAt(index);
            _stepsList?.Rebuild();
            RefreshStepSummary();
        }

        private void RunStep(int index)
        {
            if (index < 0 || index >= _steps.Count)
                return;
            ActionStepDefinition definition = _steps[index];
            ActionStep step = ActionStepRegistry.GetStep(definition.Key);
            if (step == null)
                return;
            try
            {
                Dictionary<string, string> variables = BuildAvailableVariables(index);
                if (step is SetTextVariableStep &&
                    SetTextVariableStep.TryExtractVariable(definition.Values, out string variableName, out string variableValue))
                {
                    variables[variableName] = VariableResolver.ReplaceVariables(variableValue ?? string.Empty, variables);
                }
                List<ParameterValue> resolved = ActionRunner.ResolveParameterVariables(
                    definition.Values,
                    step.Parameters,
                    step,
                    variables);
                step.Run(resolved);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Error Running Step", $"Failed to run step: {exception.Message}", "OK");
            }
        }

        private string GetStepSummary()
        {
            return _steps.Count == 0
                ? "No steps yet. Add the first step to build this workflow."
                : $"{_steps.Count} step{(_steps.Count == 1 ? string.Empty : "s")} · drag cards to reorder";
        }

        private void RefreshStepSummary()
        {
            if (_stepsSummary != null)
                _stepsSummary.text = GetStepSummary();
        }

        private async void RunAction()
        {
            ActionRunner runner = new ActionRunner(_repository);
            await runner.RunAction(_action.Id);
            AssetDatabase.Refresh();
        }

        private void Save()
        {
            _repository.SaveAction(_action);

            for (int i = 0; i < _steps.Count; i++)
            {
                ActionStepDefinition step = _steps[i];
                step.OrderIndex = i;
                step.ActionId = _action.Id;
                _repository.SaveStep(step);
            }

            // Delete removed steps
            _repository.DeleteStepsExcept(_action.Id, _steps.Where(s => s.Id > 0).Select(s => s.Id).ToList());

            _repository.Save();

            _onSave?.Invoke();
        }

        private void AddStep(ActionStep step)
        {
            ActionStepDefinition newStep = new ActionStepDefinition
            {
                Key = step.Key,
                ActionId = _action.Id,
                OrderIndex = _steps.Count,
                Values = step.Parameters.Select(p => new ParameterValue(p.DefaultValue)).ToList()
            };

            int selectedStepIndex = _stepsList?.selectedIndex ?? -1;
            if (selectedStepIndex >= 0)
            {
                _steps.Insert(selectedStepIndex + 1, newStep);
            }
            else
            {
                _steps.Add(newStep);
            }
            _stepsList?.Rebuild();
            RefreshStepSummary();
        }

        private Dictionary<string, string> BuildAvailableVariables(int stepIndex)
        {
            Dictionary<string, string> variables = new Dictionary<string, string>();

            for (int i = 0; i < stepIndex && i < _steps.Count; i++)
            {
                ActionStepDefinition stepDef = _steps[i];
                ActionStep step = ActionStepRegistry.GetStep(stepDef.Key);
                if (step == null) continue;

                if (step is SetTextVariableStep)
                {
                    if (SetTextVariableStep.TryExtractVariable(stepDef.Values, out string varName, out string varValue))
                    {
                        varValue = VariableResolver.ReplaceVariables(varValue ?? "", variables);
                        variables[varName] = varValue;
                    }
                }
            }

            return variables;
        }
    }
}

using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace SephiriaOne
{
    public sealed class ModChatCommands : MonoBehaviour
    {
        private UI_ChatInput chat;
        private TMP_InputField input;
        private TMP_InputField.SubmitEvent submit;
        private bool bindingUnsupported;

        private void Update()
        {
            if (bindingUnsupported)
            {
                return;
            }

            UIManager ui = UIManager.Instance;
            UI_ChatInput nextChat = ui ? ui.GetElement<UI_ChatInput>() : null;
            TMP_InputField nextInput = nextChat ? nextChat.inputField : null;
            TMP_InputField.SubmitEvent nextSubmit = nextInput ? nextInput.onSubmit : null;
            if (chat == nextChat && input == nextInput && ReferenceEquals(submit, nextSubmit))
            {
                return;
            }

            Unbind();
            if (!nextChat || !nextInput || nextSubmit == null)
            {
                return;
            }

            try
            {
                MethodInfo method = typeof(UI_ChatInput).GetMethod("OnSubmitCommand",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                if (method == null)
                {
                    throw new MissingMethodException("UI_ChatInput.OnSubmitCommand(string)");
                }

                var original = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), nextChat, method);
                chat = nextChat;
                input = nextInput;
                submit = nextSubmit;

                // If chat is already open, move only its vanilla runtime handler
                // behind ours. Future OnOpened calls naturally append it after us.
                submit.RemoveListener(original);
                submit.AddListener(OnSubmitted);
                if (chat.IsOpened)
                {
                    submit.AddListener(original);
                }

                Debug.Log("[SephiriaOne] Chat commands bound: /fountain, /choices, /stats");
            }
            catch (Exception exception)
            {
                Unbind();
                bindingUnsupported = true;
                Debug.LogError($"[SephiriaOne] Chat commands unavailable; chat API changed: {exception}");
            }
        }

        private void OnSubmitted(string submittedText)
        {
            if (!chat || !input)
            {
                return;
            }

            FountainParseResult result = FountainCommand.Parse(input.text, out FountainCommand command, out string error);
            ChoiceParseResult choices = ChoiceCommand.Parse(input.text, out ChoiceCommand choiceCommand, out string choiceError);
            StatParseResult stats = StatCommand.Parse(input.text, out StatCommand statCommand, out string statError);
            bool fountain = result != FountainParseResult.NotCommand;
            bool choice = choices != ChoiceParseResult.NotCommand;
            if (!fountain && !choice && stats == StatParseResult.NotCommand)
            {
                return;
            }

            // The vanilla handler reads input.text after our listener returns.
            // Clear before any work so even rejected commands stay local.
            input.text = "";
            chat.Close();
            if (stats == StatParseResult.List)
            {
                foreach (StatDefinition stat in StatCatalog.All)
                    Reply($"{stat.Name}: {stat.Minimum}..{stat.Maximum} {stat.Unit}; " +
                        (stat.Scale == 100 ? "up to 2 decimal places." : "whole numbers."), Color.cyan);
                return;
            }
            if (result == FountainParseResult.Help || choices == ChoiceParseResult.Help || stats == StatParseResult.Help)
            {
                Reply(fountain ? FountainCommand.Usage : choice ? ChoiceCommand.Usage : StatCommand.Usage, Color.cyan);
                return;
            }

            if (result == FountainParseResult.Invalid || choices == ChoiceParseResult.Invalid || stats == StatParseResult.Invalid)
            {
                Reply(fountain ? error : choice ? choiceError : statError, Color.yellow);
                return;
            }

            try
            {
                string message;
                bool success = fountain ? FountainPoints.TryExecute(command, out message) :
                    choice ? ChoicePoints.TryExecute(choiceCommand, out message) :
                    CharacterStats.TryExecute(statCommand, out message);
                Reply(message, success ? Color.green : Color.yellow);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SephiriaOne] Chat command failed: {exception}");
                Reply("Command failed. Check Player.log for details.", Color.red);
            }
        }

        private static void Reply(string message, Color color)
        {
            string text = "[SephiriaOne] " + message;
            Debug.Log(text);
            if (GameLogWriter.Instance)
            {
                GameLogWriter.Instance.WriteLog(text, color);
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Unbind()
        {
            submit?.RemoveListener(OnSubmitted);
            submit = null;
            input = null;
            chat = null;
        }
    }
}

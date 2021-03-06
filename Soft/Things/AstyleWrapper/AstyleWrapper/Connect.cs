using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;

namespace AstyleWrapper
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
        private DTE2 _applicationObject;
        private AddIn _addInInstance;
        private EnvDTE.TextEditorEvents _textEditorEvents;

        const string _execCommandStr = "AstyleWrapperIExecute";
        const string _switchCommandStr = "AstyleWrapperISwitch";
        const string _optionsCommandStr = "AstyleWrapperIOptions";
        const string _switchOffStr = "Current status: working";
        const string _switchOnStr = "Current status: disabled";
        const string _cmdPrefix = "AstyleWrapper.Connect.";
        const string _toolBarName = "Astyle wrapper toolbar";

        CommandBarButton _execCommandBtn;
        CommandBarButton _switchCommandBtn;
        CommandBarButton _optionsCommandBtn;

        HashSet<int> _hashes;
        AStyleInterface AStyle;

		/// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
		public Connect()
		{
		}

		/// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
		/// <param term='application'>Root object of the host application.</param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
		/// <param term='addInInst'>Object representing this Add-in.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;

            AStyle = new AStyleInterface();
            AStyle.LoadDefaults();
            AStyleInterface.LoadSettings(ref AStyle);

            if (connectMode == ext_ConnectMode.ext_cm_UISetup)
            {
                object[] contextGUIDS = new object[] { };
                Commands2 commands = (Commands2)_applicationObject.Commands;

                #region Set up toolbar

                var commandBars = (CommandBars)_applicationObject.CommandBars;

                // Add a new toolbar 
                CommandBar myTemporaryToolbar = null;
                try
                {
                    myTemporaryToolbar = commandBars[_toolBarName];
                }
                catch { }

                if (myTemporaryToolbar == null)
                    myTemporaryToolbar = commandBars.Add(_toolBarName, MsoBarPosition.msoBarTop, System.Type.Missing, false);
                
                Command optionsCommand = commands.AddNamedCommand2(_addInInstance, _optionsCommandStr, "Options", "Configure AstyleWrapper",
                     true, 2946, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                     (int)vsCommandStyle.vsCommandStylePict, vsCommandControlType.vsCommandControlTypeButton);

                Command executeCommand = commands.AddNamedCommand2(_addInInstance, _execCommandStr, "Format selection", "Format selection",
                     true, 611, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                     (int)vsCommandStyle.vsCommandStylePict, vsCommandControlType.vsCommandControlTypeButton);

                Command switchCommand = commands.AddNamedCommand2(_addInInstance, _switchCommandStr, "Switcher", AStyle.working ? _switchOffStr : _switchOnStr,
                    true, AStyle.working ? 1087 : 1088, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                     (int)vsCommandStyle.vsCommandStylePict, vsCommandControlType.vsCommandControlTypeButton);

                // Add a new button on that toolbar

                //_switchCommandBtn = (CommandBarButton)switchCommand.AddControl(myTemporaryToolbar, myTemporaryToolbar.Controls.Count + 1);
                _switchCommandBtn = (CommandBarButton)switchCommand.AddControl(myTemporaryToolbar, myTemporaryToolbar.Controls.Count + 1);
                _optionsCommandBtn = (CommandBarButton)optionsCommand.AddControl(myTemporaryToolbar, myTemporaryToolbar.Controls.Count + 1);
                _execCommandBtn = (CommandBarButton)executeCommand.AddControl(myTemporaryToolbar, myTemporaryToolbar.Controls.Count + 1);

                // Make visible the toolbar
                myTemporaryToolbar.Visible = true;

                #endregion
            }
            else
            {
                // try get buttons
                var commandBars = (CommandBars)_applicationObject.CommandBars;
                try
                {
                    var myTemporaryToolbar = commandBars[_toolBarName];
                    _switchCommandBtn = (CommandBarButton)myTemporaryToolbar.Controls[1];
                    _execCommandBtn = (CommandBarButton)myTemporaryToolbar.Controls[2];
                    _optionsCommandBtn = (CommandBarButton)myTemporaryToolbar.Controls[3];
                }
                catch { }

                _hashes = new HashSet<int>();

                _textEditorEvents = _applicationObject.Events.get_TextEditorEvents(null);
                _textEditorEvents.LineChanged += new _dispTextEditorEvents_LineChangedEventHandler(_textEditorEvents_LineChanged);
            }
		}

        void Execute(TextPoint StartPoint, TextPoint EndPoint, bool force)
        {
            TextDocument doc = (TextDocument)(_applicationObject.ActiveDocument.
                           Object("TextDocument"));

            //if (StartPoint.Line == EndPoint.Line && StartPoint.LineCharOffset == EndPoint.LineCharOffset)
            //    return;

            if (AStyle.fileMode ==  AStyleInterface.AstyleFilemode.CPP && doc.Language != "C/C++")
                return;

            if (AStyle.fileMode == AStyleInterface.AstyleFilemode.JAVA && doc.Language != "Java")
                return;

            if (AStyle.fileMode == AStyleInterface.AstyleFilemode.SHARP && doc.Language != "C#")
                return;

            if (!force && !AStyle.working)
                return;

            EditPoint startEditPoint = StartPoint.CreateEditPoint();
            EditPoint curEditPoint = StartPoint.CreateEditPoint();
            EditPoint endEditPoint = EndPoint.CreateEditPoint();
            startEditPoint.StartOfLine();
            curEditPoint.StartOfLine();            
            endEditPoint.EndOfLine();
            string text = curEditPoint.GetText(endEditPoint);

            String textOut = AStyle.FormatSource(text, AStyleInterface.AstyleFilemode.SHARP);

            int hashik = text.GetHashCode() ^ endEditPoint.AbsoluteCharOffset;

            if(_hashes.Contains(hashik))
                return;

            if (textOut.TrimStart('\t') == text.TrimStart('\t'))
                return;

            if (_hashes.Count > 1000000)
                _hashes.Clear(); // no memory licking!

            _hashes.Add(hashik);

            var curOffset = doc.Selection.ActivePoint.AbsoluteCharOffset;
            int beginLinePos = curEditPoint.AbsoluteCharOffset;
            int beforeLength = endEditPoint.AbsoluteCharOffset - startEditPoint.AbsoluteCharOffset;

            curEditPoint.Delete(endEditPoint);
            curEditPoint.Insert(textOut);
            curEditPoint.MoveToAbsoluteOffset(startEditPoint.AbsoluteCharOffset + textOut.Length);

            doc.Selection.Cancel();
            doc.Selection.MoveToPoint(startEditPoint, false);
            doc.Selection.MoveToPoint(curEditPoint, true);
            _applicationObject.ExecuteCommand("Edit.FormatSelection", "");

            curEditPoint.EndOfLine();

            if (curOffset > beginLinePos)
            {
                doc.Selection.MoveToAbsoluteOffset(curOffset - beforeLength
                    + (curEditPoint.AbsoluteCharOffset - startEditPoint.AbsoluteCharOffset), false);
                _applicationObject.ExecuteCommand("Edit.FormatSelection", "");
            }
            else
                doc.Selection.MoveToAbsoluteOffset(curOffset, false);
        }

        void _textEditorEvents_LineChanged(TextPoint StartPoint, TextPoint EndPoint, int Hint)
        {
            Execute(StartPoint, EndPoint, false);
        }

		/// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
		}

		/// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
            AStyle.SaveSettings();
		}
		
		/// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
		/// <param term='commandName'>The name of the command to determine state for.</param>
		/// <param term='neededText'>Text that is needed for the command.</param>
		/// <param term='status'>The state of the command in the user interface.</param>
		/// <param term='commandText'>Text requested by the neededText parameter.</param>
		/// <seealso class='Exec' />
		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
		{
			if(neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
                if(_applicationObject.ActiveDocument == null)
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusUnsupported;
				else status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
                return;
			}
		}

		/// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
		/// <param term='commandName'>The name of the command to execute.</param>
		/// <param term='executeOption'>Describes how the command should be run.</param>
		/// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
		/// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
		/// <param term='handled'>Informs the caller if the command was handled or not.</param>
		/// <seealso class='Exec' />
		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
		{
			handled = false;
			if(executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
                if (commandName == _cmdPrefix + _switchCommandStr)
                {
                    SwitchWorking();
                }
                else if (commandName == _cmdPrefix + _execCommandStr)
                {
                    TextDocument doc = (TextDocument)(_applicationObject.ActiveDocument.
                           Object("TextDocument"));
                    Execute(doc.Selection.TopPoint, doc.Selection.BottomPoint, true);
                }
                else if (commandName == _cmdPrefix + _optionsCommandStr)
                {
                    SettingsForm form = new SettingsForm(AStyle);
                    form.ShowDialog();
                }

				handled = true;
				return;
			}
		}

        private void SwitchWorking()
        {
            if (AStyle.working)
            {
                AStyle.working = false;
                _switchCommandBtn.FaceId = 1088;
                _switchCommandBtn.TooltipText = _switchOnStr;
            }
            else
            {
                AStyle.working = true;
                _switchCommandBtn.FaceId = 1087;
                _switchCommandBtn.TooltipText = _switchOffStr;
            }
        }
		
	}
}
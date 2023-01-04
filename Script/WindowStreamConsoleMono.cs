using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;

public class WindowStreamConsoleMono : MonoBehaviour
{
    public WindowStreamConsole m_console;
    public bool m_autoStartAtAwake =true;
    private void Awake()
    {
        if (m_autoStartAtAwake) 
            StartConsole();
    }
    public void Update()
    {
        m_console.BroadcastInQueueInfoFromThreads();
    }

    public void StartConsole() {
        TryToKillConsole();
        m_console.StartProcess();
    }
    
    public void TryToKillConsole() {
        m_console.TryToKill();
    }

    void OnDestroy()
    {
        TryToKillConsole();
    }

    void OnApplicationQuit()
    {
        TryToKillConsole();
    }
    public void PushCommand(string command)
    {
        m_console.PushTextAsInputLine(command);
    }
    public void PushCommandSplitByLineReturn(string command)
    {
        PushCommand(command.Split("\n"));
    }
    public void PushCommand(string [] commands)
    {
        for (int i = 0; i < commands.Length; i++)
        {
            if (commands[i] != null)
                PushCommand(commands[i]);
        }

    }

    [ContextMenu("Test_PushAdbDevices")]
    public void Test_PushAdbDevices()
    {
        m_console.PushTextAsInputLine("adb devices");
    }
    [ContextMenu("Test_PushGitHelp")]
    public void Test_PushGitHelp()
    {
        m_console.PushTextAsInputLine("git help");
    }

}

[System.Serializable]
public class WindowStreamConsole
{
    public string m_executionFolderPath;
    Process process = null;
    StreamWriter messageStream;

    public string m_title = "Unity Console Stream Communication";
    public bool m_showWindow = false;
    public StringEvent m_sentText;
    public StringEvent m_receivedText;
    public StringEvent m_sentAndReceivedText;
    public StringEvent m_receivedError;
    public MessageTransitionEvent m_onMessageTransaction;

    public DebugHistory m_debugHistory = new DebugHistory();
    [System.Serializable]
    public class DebugHistory {
        public Eloi.StringClampHistory m_sentHistory;
        public Eloi.StringClampHistory m_receivedReturnHistory;
        public Eloi.StringClampHistory m_receivedErrorHistory;
    }
    public Queue<MessageContainer> m_threadSaveQueueDebugger = new Queue<MessageContainer>();
    public void SetWorkingDirectoryPath(string workingDirectoryPath) {
        m_executionFolderPath = workingDirectoryPath;
    }

    [System.Serializable]
    public class StringEvent : UnityEvent<string> { }


    [System.Serializable]
    public class MessageTransitionEvent : UnityEvent<MessageContainer> { }

    public string m_error_consoleNotSetup= "You can't use it if you don't initialized/start it.";
    public void PushTextAsInputLine(string text) {
        if (process == null || messageStream == null)
            throw new Exception(m_error_consoleNotSetup );
        messageStream.WriteLine(text);
        messageStream.Flush();
        m_threadSaveQueueDebugger.Enqueue(new MessageContainer(MessageType.Sent, text));
        m_debugHistory.m_sentHistory.PushIn(text);
    }
    public void StartProcess()
    {
        try
        {
            process = new Process();
            process.StartInfo = new ProcessStartInfo(@"C:\WINDOWS\system32\cmd.exe");
            
            process.EnableRaisingEvents = false;
            m_executionFolderPath.Trim();
            string workingDirectory = Application.dataPath;
            if (!string.IsNullOrEmpty(m_executionFolderPath.Trim())) {
                workingDirectory = m_executionFolderPath.Trim();
            }
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardError = true;
            
                process.StartInfo.CreateNoWindow = !m_showWindow;
                process.StartInfo.WindowStyle = m_showWindow?ProcessWindowStyle.Minimized:ProcessWindowStyle.Hidden;
            

            process.OutputDataReceived += new DataReceivedEventHandler(DataReceived);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorReceived);
            process.Start();

            IntPtr windowPointer = process.MainWindowHandle;
            SetWindowTitle(windowPointer, m_title);
            process.BeginOutputReadLine();
//            process.BeginErrorReadLine();
            messageStream = process.StandardInput;


        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Unable to launch window stream console: " + e.Message);
        }
    }

    private void SetWindowSize(IntPtr windowPointer, int width, int height)
    {
        MoveWindow(windowPointer, 10, 10, width, height,true);
    }


    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    static extern bool SetWindowText(IntPtr hWnd, string text);
    private void SetWindowTitle(IntPtr windowPointer, string title)
    {
        SetWindowText(windowPointer, title);
    }

    public enum MessageType { Sent, ReceivedMessage, ReceivedError}

    [System.Serializable]
    public struct MessageContainer {

        public MessageType m_messageType;
        public string m_messageAsString;
        
        public MessageContainer(MessageType received, string data)
        {
            this.m_messageType = received;
            this.m_messageAsString = data;
        }
    }
    public void BroadcastInQueueInfoFromThreads() {

        while (m_threadSaveQueueDebugger.Count > 0) {
            MessageContainer log = m_threadSaveQueueDebugger.Dequeue();
            if (log.m_messageType == MessageType.Sent)
                m_sentText.Invoke(log.m_messageAsString);
            if (log.m_messageType == MessageType.ReceivedMessage)
                m_receivedText.Invoke(log.m_messageAsString);
            if (log.m_messageType == MessageType.Sent || log.m_messageType == MessageType.ReceivedMessage)
                m_sentAndReceivedText.Invoke(log.m_messageAsString);
            if (log.m_messageType == MessageType.ReceivedError)
                m_receivedError.Invoke(log.m_messageAsString);
        }
    }
    void DataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        m_threadSaveQueueDebugger.Enqueue(new  MessageContainer(MessageType.ReceivedMessage,   eventArgs.Data));
        m_debugHistory.m_receivedReturnHistory.PushIn(eventArgs.Data);
    }
    void ErrorReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        m_threadSaveQueueDebugger.Enqueue(new MessageContainer(MessageType.ReceivedError, eventArgs.Data));
        m_debugHistory.m_receivedErrorHistory.PushIn(eventArgs.Data);
    }

    public void TryToKill()
    {
        if (process != null && process.HasExited )
        {
            try
            {
                process.Kill();
                process.Dispose();
            }
            catch (InvalidOperationException) { 
                // If the use close the process
            }
        }
    }
}

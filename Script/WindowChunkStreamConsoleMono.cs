using Eloi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;

public class WindowChunkStreamConsoleMono : MonoBehaviour
{
    public WindowStreamConsoleMono m_consoleStream;

    public void PushCommandsSplitByLineReturn(string command)
    {
        PushCommands(command.Split("\n"));
    }

    public void PushCommands(string [] commands)
    {
        for (int i = 0; i < commands.Length; i++)
        {
            if (commands[i] != null) {
                m_queueOfCommandstoProcess.Add(new CommandToProcess( commands[i]));
            }

        }

    }
    public List<CommandToProcess> m_queueOfCommandstoProcess;
    public CommandProcessInfo m_processing;
    public CommandProcessHistory m_processedCommands;
    public CommandProcessingEvent m_onStartProcessingCommand;
    public CommandProcessingEvent m_onFinishToProcessCommand;
    public CommandWithCallbackChunkEvent m_onCallbackChunkFound;

    [System.Serializable]
    public class CommandProcessHistory : GenericClampHistory<CommandProcessInfo> { }
    [System.Serializable]
    public class CommandProcessingEvent : UnityEvent<CommandProcessInfo> { }
    [System.Serializable]
    public class CommandWithCallbackChunkEvent : UnityEvent<CommandWithCallbackChunk> { }
    public void Awake()
    {
        m_consoleStream.m_console.m_receivedText.AddListener(ListenToReturn);
        StartCoroutine(BlockOfRequestCoroutine());
    }
    private void OnDestroy()
    {
        m_consoleStream.m_console.m_receivedText.RemoveListener(ListenToReturn);
    }

    private void ListenToReturn(string arg0)
    {
        m_callBackTrack.Add( arg0);
    }

    public IEnumerator BlockOfRequestCoroutine() {

        while (true) {
            yield return new WaitForEndOfFrame();
            if (m_queueOfCommandstoProcess.Count > 0) {
                CommandToProcess toProcess = m_queueOfCommandstoProcess[0];
                m_queueOfCommandstoProcess.RemoveAt(0);
                m_processing = new CommandProcessInfo() { m_commandToProcess = toProcess };
                m_processing.m_processState = CommandProcessInfo.ProcessStateType.Processing;

                yield return StartToProcess(toProcess, m_processing);
                m_processing.m_processState = CommandProcessInfo.ProcessStateType.Processed;
                m_processedCommands.PushIn(m_processing);
                m_onCallbackChunkFound.Invoke(new CommandWithCallbackChunk()
                {
                    m_commandProcessed = m_processing.m_commandToProcess.m_command,
                    m_callbackMessage = m_processing.m_messageReceived,
                    m_startTime = m_processing.m_sentToConsoleTime,
                    m_endTime = m_processing.m_processedByConsoleTime,
                    m_processingTimeInSeconds = m_processing.m_processingTimeInSeconds
                });;
            }


        }
    }
    public void SkipCurrentProcess() { m_skipCurrentProcess = true; }
    public List<string> m_callBackTrack= new List<string>();
    public bool m_skipCurrentProcess;
    public string m_endIdKey= "cmd 95222290dd7278aa3ddd389cc1e1d165cc4bafe5";
    private IEnumerator StartToProcess(CommandToProcess toProcess,  CommandProcessInfo info)
    {
        info.m_sentToConsoleTime = DateTime.Now;
       
        bool cmdEndFound = false;
        m_callBackTrack.Clear();
        m_consoleStream.PushCommand(new string[] { toProcess.m_command, m_endIdKey });
        while (!cmdEndFound) {

            if (m_skipCurrentProcess)
                break;
            for (int i = 0; i < m_callBackTrack.Count; i++)
            {
                if (IsMicrosoftCorporationLine(i))
                {
                    cmdEndFound = true;

                    break;
                }

            }
            if(!cmdEndFound)
                yield return new WaitForEndOfFrame();

            info.m_processingTimeInSeconds = (DateTime.Now - info.m_sentToConsoleTime).Seconds;
        }

        RemoteAllAfterCMDCommand(ref m_callBackTrack);
        m_skipCurrentProcess = false;
        m_processing.m_processedByConsoleTime = DateTime.Now;
        yield break;
    }

    private void RemoteAllAfterCMDCommand(ref List<string> m_callBackTrack)
    {

        
        for (int i = m_callBackTrack.Count - 1; i >=0 ; i--)
        {
            Eloi.E_CodeTag.DirtyCode.Info("Really can do better, but I am sleepy and need to see it works before continuing");
            if (m_callBackTrack[i].Trim().IndexOf(m_endIdKey) > -1)
            {
                m_callBackTrack.RemoveAt(i);
                return;
            }
            else { 
                m_callBackTrack.RemoveAt(i);     
            
            }
        }
    }

    private bool IsMicrosoftCorporationLine(int i)
    {
        return m_callBackTrack[i].Length > 3
                            && m_callBackTrack[i][0] == '('
                            && (m_callBackTrack[i][1] == 'c'
                            || m_callBackTrack[i][1] == 'C')
                            && m_callBackTrack[i][2] == ')'
                            && m_callBackTrack[i].ToLower().IndexOf("microsoft") > -1;
    }

    [System.Serializable]
    public class CommandToProcess {
        public string m_command;

        public CommandToProcess(string command)
        {
            m_command = command;
        }
    }

    [System.Serializable]
    public class CommandProcessInfo {
        public CommandToProcess m_commandToProcess;
        public enum ProcessStateType { InQueue, Processing, Processed}
        public ProcessStateType m_processState;
        public string [] m_messageReceived;
        public DateTime m_sentToConsoleTime;
        public DateTime m_processedByConsoleTime;
        public float m_processingTimeInSeconds = 0;
    }

    [System.Serializable]
    public class CommandWithCallbackChunk
    {
        public string m_commandProcessed;
        public string[] m_callbackMessage;
        public DateTime m_startTime;
        public DateTime m_endTime;
        public float m_processingTimeInSeconds = 0;
    }

}

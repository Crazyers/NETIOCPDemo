using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnalysisSvr;

namespace AnalysisSvr
{
    /// <summary>
    /// 协议组装器，用来组装往外发送的命令，主要用于组装协议格式
    /// </summary>
    public class OutgoingDataAssembler
    {
        private List<string> m_protocolText;

        public OutgoingDataAssembler()
        {
            m_protocolText = new List<string>();
        }

        public void Clear()
        {
            m_protocolText.Clear();
        }

        /// <summary>
        /// 把List里的值转为用换行符分隔的字符串
        /// </summary>
        /// <returns></returns>
        public string GetProtocolText()
        {
            string tmpStr = "";
            //[Request]
            //Command=Login
            //UserName=admin
            //Password=21232f297a57a5a743894a0e4a801fc3
            if (m_protocolText.Count > 0)
            {
                tmpStr = m_protocolText[0];
                for (int i = 1; i < m_protocolText.Count; i++)
                {
                    //每一个命令都有一个头：即m_protocolText[0]
                    tmpStr = tmpStr + ProtocolKey.ReturnWrap + m_protocolText[i];
                }
            }
            return tmpStr;
        }

        public void AddRequest()
        {
            m_protocolText.Add(ProtocolKey.LeftBrackets + ProtocolKey.Request + ProtocolKey.RightBrackets);
        }

        public void AddResponse()
        {
            m_protocolText.Add(ProtocolKey.LeftBrackets + ProtocolKey.Response + ProtocolKey.RightBrackets);
        }

        public void AddCommand(string commandKey)
        {
            m_protocolText.Add(ProtocolKey.Command + ProtocolKey.EqualSign + commandKey);
        }

        public void AddSuccess()
        {
            m_protocolText.Add(ProtocolKey.Code + ProtocolKey.EqualSign + ProtocolCode.Success.ToString());
        }

        public void AddFailure(int errorCode, string message)
        {
            m_protocolText.Add(ProtocolKey.Code + ProtocolKey.EqualSign + errorCode.ToString());
            m_protocolText.Add(ProtocolKey.Message + ProtocolKey.EqualSign + message);
        }

        public void AddValue(string protocolKey, string value)
        {
            m_protocolText.Add(protocolKey + ProtocolKey.EqualSign + value);
        }

        public void AddValue(string protocolKey, short value)
        {
            m_protocolText.Add(protocolKey + ProtocolKey.EqualSign + value.ToString());
        }

        public void AddValue(string protocolKey, int value)
        {
            m_protocolText.Add(protocolKey + ProtocolKey.EqualSign + value.ToString());
        }

        public void AddValue(string protocolKey, long value)
        {
            m_protocolText.Add(protocolKey + ProtocolKey.EqualSign + value.ToString());
        }

        public void AddValue(string protocolKey, Single value)
        {
            m_protocolText.Add(protocolKey + ProtocolKey.EqualSign + value.ToString());
        }

        public void AddValue(string protocolKey, double value)
        {
            m_protocolText.Add(protocolKey + ProtocolKey.EqualSign + value.ToString());
        }
    }
}

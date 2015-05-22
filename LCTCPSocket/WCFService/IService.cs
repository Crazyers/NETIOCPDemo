using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace WCFLibrary
{
    // 注意: 使用“重构”菜单上的“重命名”命令，可以同时更改代码和配置文件中的接口名“IService”。
    ////力创Socket 服务接口
    [ServiceContract]
    public interface ILCSocketService
    {
        [OperationContract]
         string ShowName(string name);

    }
}



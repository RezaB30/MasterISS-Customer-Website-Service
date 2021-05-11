using RadiusR.API.CustomerWebService.Requests;
using RadiusR.API.CustomerWebService.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace RadiusR.API.CustomerWebService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "INetspeedCustomerService" in both code and config file together.
    [ServiceContract]
    public interface INetspeedCustomerService : IGenericCustomerService, RadiusR.API.TelekomInfrastructureService.ITelekomInfrastructureService
    {
        [OperationContract]
        CustomerServiceExistingCustomerRegisterResponse ExistingCustomerRegister(CustomerServiceExistingCustomerRegisterRequest request);
        [OperationContract]
        CustomerServiceAppLogResponse AppLog(CustomerServiceAppLogRequest request);
    }
}

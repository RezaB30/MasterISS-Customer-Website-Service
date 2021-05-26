using RadiusR.API.CustomerWebService.Requests.AgentRequests;
using RadiusR.API.CustomerWebService.Responses.AgentResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace RadiusR.API.CustomerWebService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IAgentWebService" in both code and config file together.
    [ServiceContract]
    public interface IAgentWebService
    {
        [OperationContract]
        string GetKeyFragment(string username);
        [OperationContract]
        AgentServiceAuthenticationResponse Authenticate(AgentServiceAuthenticationRequest request);
        [OperationContract]
        AgentServiceNewCustomerRegisterResponse NewCustomerRegister(AgentServiceNewCustomerRegisterRequest request);
        [OperationContract]
        AgentServiceSMSCodeResponse SendConfirmationSMS(AgentServiceSMSCodeRequest request);
        [OperationContract]
        AgentServiceSubscriptionsResponse GetAgentSubscriptions(AgentServiceSubscriptionsRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetCultures(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetTCKTypes(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetCustomerTypes(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetSexes(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetNationalities(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetProfessions(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetAgentTariffs(AgentServiceParameterlessRequest request);
        [OperationContract]
        AgentServiceKeyValueListResponse GetPaymentDays(AgentServiceListFromIDRequest request);
        [OperationContract]
        AgentServicePaymentResponse PayBills(AgentServicePaymentRequest request);
        [OperationContract]
        AgentServiceCredentialSMSResponse SendCredentialSMS(AgentServiceCredentialSMSRequest request);
    }
}

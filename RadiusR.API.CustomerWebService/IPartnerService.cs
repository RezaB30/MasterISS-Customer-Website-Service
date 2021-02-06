using RadiusR.API.CustomerWebService.Requests.PartnerRequests;
using RadiusR.API.CustomerWebService.Responses.PartnerResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace RadiusR.API.CustomerWebService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IPartnerService" in both code and config file together.
    [ServiceContract]
    public interface IPartnerService : RadiusR.API.TelekomInfrastructureService.ITelekomInfrastructureService
    {
        [OperationContract]
        string GetKeyFragment(string username);
        [OperationContract]
        PartnerServicePaymentResponse PayBills(PartnerServicePaymentRequest request);
        [OperationContract]
        PartnerServiceAuthenticationResponse Authenticate(PartnerServiceAuthenticationRequest request);

    }
}

using System.Security.Claims;
using Qms.Domain.Collection;

namespace Qms.Application.Collection;

public interface ICepAnalysisService
{
    Task<CepAnalysis> RunAsync(int datasetId, ClaimsPrincipal user);
    Task<IReadOnlyList<CepAnalysis>> ListAsync(int? datasetId, ClaimsPrincipal user);
    Task<CepAnalysis> GetAsync(int id, ClaimsPrincipal user);
}

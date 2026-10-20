using System.Security.Claims;
using Qms.Domain.Collection;
using Qms.Statistics;

namespace Qms.Application.Collection;

public sealed record CapabilityStudyResult(CapabilityResult Result, double CpTarget, double CpkTarget, string CpClass, string CpkClass);

public interface ICapabilityService
{
    Task<CapabilityStudy> RunAsync(int datasetId, ClaimsPrincipal user);
    Task<IReadOnlyList<CapabilityStudy>> ListAsync(ClaimsPrincipal user);
}

public interface IStudyFileReader
{
    Task<IReadOnlyList<string[]>> ReadAsync(string filename, Stream stream, int maximumSamples);
}

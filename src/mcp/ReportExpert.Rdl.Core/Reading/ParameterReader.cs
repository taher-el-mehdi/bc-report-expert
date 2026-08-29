using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Reading;

/// <summary>
/// Read-only access to the parameters of a report definition.
/// </summary>
public static class ParameterReader
{
    /// <summary>
    /// Reads every report parameter with its type, prompt, defaults and allowed values.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <returns>The parameters, in document order.</returns>
    public static ParametersResult GetParameters(RdlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;
        var parameters = new List<ParameterInfo>();

        foreach (var parameter in document.Descendants("ReportParameter"))
        {
            IReadOnlyList<string>? defaults = null;

            var defaultValue = parameter.Element(ns + "DefaultValue");
            if (defaultValue is not null)
            {
                var values = defaultValue
                    .Elements(ns + "Values")
                    .Elements(ns + "Value")
                    .Select(v => v.Value)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();

                if (values.Count > 0)
                    defaults = values;
            }

            string? validValuesDataSet = null;
            IReadOnlyList<ValidValue>? validValues = null;

            var validValuesElement = parameter.Element(ns + "ValidValues");
            if (validValuesElement is not null)
            {
                var dataSetReference = validValuesElement.Element(ns + "DataSetReference");
                if (dataSetReference is not null)
                    validValuesDataSet = dataSetReference.Element(ns + "DataSetName")?.Value ?? string.Empty;

                var listed = validValuesElement
                    .Elements(ns + "ParameterValues")
                    .Elements(ns + "ParameterValue")
                    .Select(pv =>
                    {
                        string value = pv.Element(ns + "Value")?.Value ?? string.Empty;
                        // RDL omits Label when it would duplicate the value.
                        string label = pv.Element(ns + "Label")?.Value ?? value;
                        return new ValidValue(value, label);
                    })
                    .ToList();

                if (listed.Count > 0)
                    validValues = listed;
            }

            parameters.Add(new ParameterInfo
            {
                Name = parameter.Attribute("Name")?.Value ?? string.Empty,
                DataType = parameter.Element(ns + "DataType")?.Value ?? "Unknown",
                Prompt = parameter.Element(ns + "Prompt")?.Value ?? string.Empty,
                DefaultValues = defaults,
                ValidValuesDataSet = validValuesDataSet,
                ValidValues = validValues,
            });
        }

        return new ParametersResult(parameters);
    }

    /// <summary>
    /// Locates a report parameter by name.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The <c>ReportParameter</c> element.</returns>
    /// <exception cref="RdlNotFoundException">No parameter carries that name.</exception>
    public static XElement RequireParameter(RdlDocument document, string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var parameters = document.Descendants("ReportParameter").ToList();

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter.Attribute("Name")?.Value, name, StringComparison.Ordinal))
                return parameter;
        }

        string available = parameters.Count == 0
            ? "the report has no parameters"
            : $"available: {string.Join(", ", parameters.Select(p => p.Attribute("Name")?.Value ?? "(unnamed)"))}";

        throw new RdlNotFoundException(
            RdlTarget.Parameter,
            $"Parameter \"{name}\" not found ({available}).");
    }
}

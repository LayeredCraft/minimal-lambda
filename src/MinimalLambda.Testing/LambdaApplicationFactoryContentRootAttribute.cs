// Portions of this file are derived from aspnetcore
// Source:
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Mvc/Mvc.Testing/src/LambdaApplicationFactoryContentRootAttribute.cs
// Copyright (c) .NET Foundation and Contributors
// Licensed under the MIT License
// See THIRD-PARTY-LICENSES.txt file in the project root or visit
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/LICENSE.txt

using System.Globalization;
using System.Reflection;

namespace MinimalLambda.Testing;

/// <summary>
///     Metadata that <see cref="LambdaApplicationFactory{TEntryPoint}" /> uses to find out the content
///     root for the web application represented by <c>TEntryPoint</c>.
///     <see cref="LambdaApplicationFactory{TEntryPoint}" /> will iterate over all the instances of
///     <see cref="LambdaApplicationFactoryContentRootAttribute" />, filter the instances whose
///     <see cref="Key" /> is equal to <c>TEntryPoint</c> <see cref="Assembly.FullName" />, order them
///     by <see cref="Priority" /> in ascending order.
///     <see cref="LambdaApplicationFactory{TEntryPoint}" /> will check for the existence of the marker
///     in
///     <c>
///         Path.Combine(<see cref="ContentRootPath" />, Path.GetFileName(<see cref="ContentRootTest" />
///         ))"
///     </c>
///     and if the file exists it will set the content root to
///     <see cref="ContentRootPath" />.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class LambdaApplicationFactoryContentRootAttribute : Attribute
{
    /// <summary>Initializes a new instance of <see cref="LambdaApplicationFactoryContentRootAttribute" />.</summary>
    /// <param name="key">
    ///     The key of this <see cref="LambdaApplicationFactoryContentRootAttribute" />. This
    ///     key is used by <see cref="LambdaApplicationFactory{TEntryPoint}" /> to determine what of the
    ///     <see cref="LambdaApplicationFactoryContentRootAttribute" /> instances on the test assembly
    ///     should be used to match a given TEntryPoint class.
    /// </param>
    /// <param name="contentRootPath">
    ///     The path to the content root. This path can be either relative or
    ///     absolute. In case the path is relative, the path will be combined with
    ///     <see cref="Directory.GetCurrentDirectory()" />
    /// </param>
    /// <param name="contentRootTest">
    ///     A file that will be use as a marker to determine that the content
    ///     root path for the given context is correct.
    /// </param>
    /// <param name="priority">
    ///     The priority of this content root attribute compared to other attributes.
    ///     When multiple <see cref="LambdaApplicationFactoryContentRootAttribute" /> instances are applied
    ///     for the same key, they are processed with <see cref="int.Parse(string)" />, ordered in
    ///     ascending order and applied in priority until a match is found.
    /// </param>
    public LambdaApplicationFactoryContentRootAttribute(
        string key,
        string contentRootPath,
        string contentRootTest,
        string priority)
    {
        Key = key;
        ContentRootPath = contentRootPath;
        ContentRootTest = contentRootTest;
        if (int.TryParse(
            priority,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedPriority))
            Priority = parsedPriority;
    }

    /// <summary>
    ///     Gets the content root path for a given project. This content root can be relative or
    ///     absolute. If it is a relative path, it will be combined with
    ///     <see cref="AppContext.BaseDirectory" />.
    /// </summary>
    public string ContentRootPath { get; }

    /// <summary>A marker file used to ensure that the path the content root is being set to is correct.</summary>
    public string ContentRootTest { get; }

    /// <summary>
    ///     Gets the key for the content root associated with this project. Typically
    ///     <see cref="Assembly.FullName" />.
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     Gets a number for determining the probing order when multiple
    ///     <see cref="LambdaApplicationFactoryContentRootAttribute" /> instances with the same key are
    ///     present on the test <see cref="Assembly" />.
    /// </summary>
    public int Priority { get; }
}

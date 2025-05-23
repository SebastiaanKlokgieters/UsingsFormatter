using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Progress;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.CodeCleanup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using System.IO;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Transactions;

namespace ReSharperPlugin.UsingsFormatter;

[CodeCleanupModule]
public class CodeCleanUpModule : ICodeCleanupModule
{
    private readonly List<IUsingSymbolDirective> _foundDirectives = [];

    private readonly List<IUsingSymbolDirective> _duplicateDirectives = [];

    public void SetDefaultSetting(CodeCleanupProfile profile, CodeCleanupService.DefaultProfileType profileType)
    {
        Console.WriteLine("Attempting to set default setting");

        profile.SetSetting(OurDescriptor, true);
        Console.WriteLine("Setting default setting successfull");
    }

    public bool IsAvailable(IPsiSourceFile sourceFile)
    {
        return sourceFile.LanguageType.Is<CSharpProjectFileType>();
    }

    public bool IsAvailable(CodeCleanupProfile profile)
    {
        return true;
    }

    public void Process(IPsiSourceFile sourceFile,
        IRangeMarker rangeMarker,
        CodeCleanupProfile profile,
        IProgressIndicator progressIndicator,
        IUserDataHolder cache)
    {
        var psiFiles = sourceFile.GetPsiFiles<CSharpLanguage>();
        progressIndicator.Start(100);
        var project = sourceFile.GetProject();
        var exists = project != null && File.Exists($"{project.GetLocation()}\\GlobalUsings.cs");

        for (int i = 0; i < psiFiles.Count; i++)
        {
            var psiFile = psiFiles[i];

            foreach (var node in psiFile.Children().Where(n => n is IUsingList))
            {
                var directives = node.Descendants<IUsingSymbolDirective>().Collect();

                foreach (var directive in directives)
                {
                    if (_foundDirectives.Exists(u =>
                            u.ImportedSymbolName.QualifiedName == directive.ImportedSymbolName.QualifiedName))
                    {
                        _duplicateDirectives.Add(directive);
                    }
                    else
                    {
                        _foundDirectives.Add(directive);
                    }
                }
            }

            progressIndicator.Advance(i * 100 / psiFiles.Count);
            i++;
        }

        if (_duplicateDirectives.Any())
        {
            if (!exists)
            {
                File.Create($"{project.GetLocation()}\\GlobalUsings.cs");
            }

            _duplicateDirectives.ForEach(d =>
            {
                var file = d.GetSourceFile();

                if (file != null)
                {
                    file.GetPsiServices().Transactions.Execute(("Delete Using Directives"),
                        () => { ModificationUtil.DeleteChild(d); }
                    );

                    Console.WriteLine("Deleted using directive");
                }
            });
        }
    }

    public string Name { get; } = "UsingsCleanUp";
    public PsiLanguageType LanguageType { get; } = CSharpLanguage.Instance;
    public ICollection<CodeCleanupOptionDescriptor> Descriptors { get; } = [OurDescriptor];
    public bool IsAvailableOnSelection { get; } = true;

    private static readonly CodeCleanupSingleOptionDescriptor OurDescriptor =
        new CodeCleanupOptionDescriptor<bool>("SortForProject",
            new CodeCleanupLanguage("SortForProject", 1),
            CodeCleanupOptionDescriptor.ReformatGroup, displayName: "Sort directives for project");
}
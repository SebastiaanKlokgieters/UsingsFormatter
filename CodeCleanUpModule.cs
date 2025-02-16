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

namespace ReSharperPlugin.UsingsFormatter;

[CodeCleanupModule]
public class CodeCleanUpModule : ICodeCleanupModule
{
    private static readonly CodeCleanupSingleOptionDescriptor OurDescriptor = new CodeCleanupOptionDescriptor<bool>(
        "UsingsCleanUp", new CodeCleanupLanguage("UsingsCleanUp", 1),
        CodeCleanupOptionDescriptor.ReformatGroup, displayName: "Anderenaam");

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
        rangeMarker = new RangeMarker(sourceFile.Document, new TextRange());
        var psiFiles = sourceFile.GetPsiFiles<CSharpLanguage>();

        foreach (var psiFile in psiFiles)
        {
            foreach (var node in psiFile.Children().Where(n => n is IUsingList))
            {
                var directives = node.Descendants<IUsingSymbolDirective>().Collect();

                foreach (var directive in directives)
                {
                    if (_foundDirectives.Exists(u => u.ImportedSymbolName.QualifiedName == directive.ImportedSymbolName.QualifiedName))
                    {
                        _duplicateDirectives.Add(directive);
                    }
                    else
                    {
                        _foundDirectives.Add(directive);
                    }
                }
            }

            return;
        }
    }

    public string Name { get; } = "UsingsCleanUp";
    public PsiLanguageType LanguageType { get; } = CSharpLanguage.Instance;
    public ICollection<CodeCleanupOptionDescriptor> Descriptors { get; } = [OurDescriptor];
    public bool IsAvailableOnSelection { get; } = true;
}
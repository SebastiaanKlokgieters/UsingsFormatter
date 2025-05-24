using System;
using System.Collections.Generic;
using JetBrains.Application.Progress;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.CodeCleanup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using System.IO;
using System.Linq;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Modules;

namespace ReSharperPlugin.UsingsFormatter;

[CodeCleanupModule]
public class CodeCleanUpModule : ICodeCleanupModule
{
    private readonly List<ITreeNode> _foundDirectives = [];

    private readonly List<ITreeNode> _duplicateDirectives = [];
    
    private int _count;
    private int _totalCount;

    public void SetDefaultSetting(CodeCleanupProfile profile, CodeCleanupService.DefaultProfileType profileType)
    {
        Console.WriteLine("Attempting to set default setting");

        profile.SetSetting(OurDescriptor, true);
        Console.WriteLine("Setting default setting successfull");
    }

    public bool IsAvailable(IPsiSourceFile sourceFile)
    {
        return sourceFile.LanguageType.Is<CSharpProjectFileType>() && sourceFile.GetProject() != null;
    }

    public bool IsAvailable(CodeCleanupProfile profile)
    {
        return profile.GetSetting(OurDescriptor).Equals(true);
    }

    public void Process(IPsiSourceFile sourceFile,
        IRangeMarker rangeMarker,
        CodeCleanupProfile profile,
        IProgressIndicator progressIndicator,
        IUserDataHolder cache)
    {
        var project = sourceFile.GetProject();
        var exists = project != null && File.Exists($"{project.GetLocation()}\\GlobalUsings.cs");

        if (_count == 0)
        {
            SetNumOfCSharpFiles(project);
            progressIndicator.CurrentItemText = "Searching for duplicate using directives";
            progressIndicator.Start(_totalCount);
        }

        if (sourceFile.LanguageType.Is<CSharpProjectFileType>() && sourceFile.IsProjFile())
        {
            var file = sourceFile.GetTheOnlyPsiFile<CSharpLanguage>();
            if (file != null)
            {
                var directives = file.Children<IUsingList>();
                var kutlijst = directives.SelectMany(d => d.Descendants<IUsingSymbolDirective>().Collect());
                _foundDirectives.AddRange(kutlijst);
            }

            progressIndicator.Advance();
            _count++;
        }

        if (_count == _totalCount)
        {
            progressIndicator.CurrentItemText = "Deleting duplicate using directives";
            progressIndicator.Advance();

            for (int i = 0; i < _foundDirectives.Count; i++)
            {
                var directive = _foundDirectives[i];
                var duplicates = _foundDirectives.FindAll(f => f.GetText() == directive.GetText());

                if (duplicates.Count > 1)
                {
                    _foundDirectives.RemoveRange(duplicates);
                    _duplicateDirectives.AddRange(duplicates);
                }
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
                            () => { ModificationUtil.DeleteChild(d); });

                        Console.WriteLine("Deleted using directive");
                    }
                });
            }
        }
    }

    private void SetNumOfCSharpFiles(IProject project)
    {
        var subItems = project.GetSubItems();

        foreach (var subItem in subItems)
        {
            if (subItem is IProjectFile projectFile)
            {
                if (Equals(projectFile.LanguageType, CSharpProjectFileType.Instance))
                {
                    _totalCount++;
                }
            }
        }
    }


    public string Name { get; } = "UsingsCleanUp";
    public PsiLanguageType LanguageType { get; } = CSharpLanguage.Instance!;
    public ICollection<CodeCleanupOptionDescriptor> Descriptors { get; } = [OurDescriptor];
    public bool IsAvailableOnSelection { get; } = true;

    private static readonly CodeCleanupSingleOptionDescriptor OurDescriptor =
        new CodeCleanupOptionDescriptor<bool>("SortForProject",
            new CodeCleanupLanguage("C#", 5),
            CodeCleanupOptionDescriptor.OptimizeImportsGroup,
            displayName: "Move Duplicate Using Directives to GlobalUsings.cs");
}
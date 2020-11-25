﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Interactive

open System
open System.IO
open System.Diagnostics
open System.Globalization
open System.Windows.Forms
open System.Runtime.InteropServices
open System.ComponentModel.Design
open Microsoft.Win32
open Microsoft.VisualStudio
open Microsoft.VisualStudio.FSharp.Interactive
open Microsoft.VisualStudio.OLE.Interop
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.Package
open Microsoft.VisualStudio.TextManager.Interop
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Utilities

open Util
open System.ComponentModel
open Microsoft.VisualStudio.FSharp.Interactive.Session

module internal ContentType = 
    [<Export>]
    [<Name(Guids.fsiContentTypeName)>]
    [<BaseDefinition("code")>]
    let FSharpContentTypeDefinition = ContentTypeDefinition()

// FsiPropertyPage
[<ComVisible(true)>]
[<CLSCompliant(false)>]
[<ClassInterface(ClassInterfaceType.AutoDual)>]
[<Guid("4489e9de-6ac1-3cd6-bff8-a904fd0e82d4")>]
type FsiPropertyPage() = 
    inherit DialogPage()

    [<ResourceCategory(SRProperties.FSharpInteractiveMisc)>]
    [<ResourceDisplayName(SRProperties.FSharpInteractive64Bit)>]
    [<ResourceDescription(SRProperties.FSharpInteractive64BitDescr)>]
    member this.FsiPreferAnyCPUVersion with get() = SessionsProperties.useAnyCpuVersion and set (x:bool) = SessionsProperties.useAnyCpuVersion <- x

    [<ResourceCategory(SRProperties.FSharpInteractiveMisc)>]
    [<ResourceDisplayName(SRProperties.FSharpInteractiveOptions)>]
    [<ResourceDescription(SRProperties.FSharpInteractiveOptionsDescr)>]
    member this.FsiCommandLineArgs with get() = SessionsProperties.fsiArgs and set (x:string) = SessionsProperties.fsiArgs <- x

    [<ResourceCategory(SRProperties.FSharpInteractiveMisc)>]
    [<ResourceDisplayName(SRProperties.FSharpInteractiveShadowCopy)>]
    [<ResourceDescription(SRProperties.FSharpInteractiveShadowCopyDescr)>]
    member this.FsiShadowCopy with get() = SessionsProperties.fsiShadowCopy and set (x:bool) = SessionsProperties.fsiShadowCopy <- x

    [<ResourceCategory(SRProperties.FSharpInteractiveDebugging)>]
    [<ResourceDisplayName(SRProperties.FSharpInteractiveDebugMode)>]
    [<ResourceDescription(SRProperties.FSharpInteractiveDebugModeDescr)>]
    member this.FsiDebugMode with get() = SessionsProperties.fsiDebugMode and set (x:bool) = SessionsProperties.fsiDebugMode <- x

    [<ResourceCategory(SRProperties.FSharpInteractivePreview)>]
    [<ResourceDisplayName(SRProperties.FSharpInteractivePreviewMode)>]
    [<ResourceDescription(SRProperties.FSharpInteractivePreviewModeDescr)>]
    member this.FsiPreview with get() = SessionsProperties.fsiPreview and set (x:bool) = SessionsProperties.fsiPreview <- x

// CompletionSet
type internal FsiCompletionSet(imageList,source:Source) =
    inherit CompletionSet(imageList, source)

// Declarations
type internal FsiDeclarations(items : (string*string*string*int)[] ) =
    inherit Declarations()
    override this.GetCount()        = items.Length
    override this.GetName(i)        = items.[i] |> (fun (n,_,_,_) -> n)
    override this.GetDescription(i) = items.[i] |> (fun (_,d,_,_) -> d)
    override this.GetDisplayText(i) = items.[i] |> (fun (_,_,dsp,_) -> dsp)
    override this.GetGlyph(i:int)   = items.[i] |> (fun (_,_,_,g) -> g)
    new() = new FsiDeclarations([||])

// Methods
type internal FsiMethods() =
    inherit Methods()
    let items = [| |]
    override this.GetCount() = items.Length
    override this.GetDescription(i) = items.[i]
    override this.GetName(i) = items.[i]
    override this.GetParameterCount(_:int) = 0
    override this.GetParameterInfo(i,_,name:byref<string>,display:byref<string>,description:byref<string>) =
        name <- items.[i]; display <- ""; description <- ""
    override this.GetType(_:int) = null:string

// FsiSource
type internal FsiSource(service:LanguageService, textLines:IVsTextLines, colorizer:Colorizer) =
    inherit Source(service, textLines, colorizer)
    override this.GetCommentFormat() = 
        let mutable info = new CommentInfo()
        info.BlockEnd<-"(*"
        info.BlockStart<-"*)"
        info.UseLineComments<-true
        info.LineStart <- "//"
        info 
    override this.CreateCompletionSet() =
        (new FsiCompletionSet(this.LanguageService.GetImageList(), this) :> CompletionSet)        
    override source.OnCommand(textView, command, ch) =
        base.OnCommand(textView, command, ch)
    override this.Completion(textView:IVsTextView,info:TokenInfo,reason:ParseReason) =
        base.Completion(textView,info,reason)

type internal FsiScanner(_buffer:IVsTextLines) =
    interface Microsoft.VisualStudio.Package.IScanner with
        override this.SetSource(_:string,_:int) = ()
        override this.ScanTokenAndProvideInfoAboutIt(_:TokenInfo,_:byref<int>) = false
            // Implementing a scanner with TokenTriggers could start intellisense calls, e.g. on DOT.

type internal FsiAuthoringScope(sessions:FsiSessions option,_readOnlySpanGetter:unit -> TextSpan) = 
    inherit AuthoringScope()
    override this.GetDataTipText(_:int,_:int,span:byref<TextSpan>) =
        span <- new TextSpan()
        null

    override this.GetDeclarations(_snapshot,_:int,_:int,_:TokenInfo,_:ParseReason) =
        match sessions with
        | None -> (new FsiDeclarations() :> Declarations)
        | Some _ ->
#if FSI_SERVER_INTELLISENSE
          if Guids.enable_fsi_intellisense then
            let lines = view.GetBuffer()                   |> throwOnFailure1
            //NOTE:
            //  There is an issue of how much preceeding text to grab for the intellisense.
            //  Ideally, we want all text from the end of the last executed interaction.
            //  However, we do not have an interactive "scanner" yet.
            //------
            // The decision is use the current "input area" as the source context.
            // Multiline input is available to a limited degree (and could be improved).
            let span = readOnlySpanGetter()
            let str   = lines.GetLineText(span.iEndLine,span.iEndIndex,line,col) |> throwOnFailure1           
            let declInfos = sessions.GetDeclarationInfos (str:string)
            new FsiDeclarations(declInfos) :> Declarations
          else
#endif
            (new FsiDeclarations() :> Declarations)

    override this.GetMethods(_:int,_:int,_:string) = 
        new FsiMethods() :> Methods

    override this.Goto(_      : VSConstants.VSStd97CmdID,
                       _ : IVsTextView,
                       _     : int,
                       _      : int,
                       span     : byref<TextSpan>) =
        span <- new TextSpan()
        null : string

type internal FsiViewFilter(mgr:CodeWindowManager,view:IVsTextView) =
    inherit ViewFilter(mgr,view)
    let isShowMemberList guidCmdGroup nCmdId = (guidCmdGroup = VSConstants.VSStd2K)&& (nCmdId = (uint32 VSConstants.VSStd2KCmdID.SHOWMEMBERLIST))    
    let isCompleteWord   guidCmdGroup nCmdId = (guidCmdGroup = VSConstants.VSStd2K)&& (nCmdId = (uint32 VSConstants.VSStd2KCmdID.COMPLETEWORD))    
    override this.Dispose() = base.Dispose()
    override this.QueryCommandStatus(guidCmdGroup:byref<Guid>,nCmdId:uint32) =        
        if isShowMemberList guidCmdGroup nCmdId || isCompleteWord guidCmdGroup nCmdId  then
            int32 (OLECMDF.OLECMDF_SUPPORTED ||| OLECMDF.OLECMDF_ENABLED)
        else
            base.QueryCommandStatus(&guidCmdGroup,nCmdId)
    override this.HandlePreExec(guidCmdGroup:byref<Guid>,nCmdId:uint32,nCmdexecopt:uint32,pvaIn:IntPtr,pvaOut:IntPtr) =
        if isShowMemberList guidCmdGroup nCmdId then
            // does it come through?
            let _line, _col = view.GetCaretPos() |> throwOnFailure2
            let _source = mgr.Source
            let tokenInfo = new TokenInfo()
            mgr.Source.Completion(view,tokenInfo,ParseReason.DisplayMemberList)
            true // handled here
        elif isCompleteWord guidCmdGroup nCmdId then
            let _line, _col = view.GetCaretPos() |> throwOnFailure2
            let _source = mgr.Source
            let tokenInfo = new TokenInfo() // Rather than MPFs source.GetTokenInfo(line,col)
            mgr.Source.Completion(view,
                                  tokenInfo,
                                  ParseReason.CompleteWord)
            true
        else            
            base.HandlePreExec(&guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut) // pass down

module internal Helpers =
    let FsiKeyword =
        { new IVsColorableItem with 
            member x.GetDefaultColors(piForeground, piBackground) =
                piForeground.[0] <- COLORINDEX.CI_BLUE
                piBackground.[0] <- COLORINDEX.CI_USERTEXT_BK
                VSConstants.S_OK

            member x.GetDefaultFontFlags(pdwFontFlags) =
                pdwFontFlags <- 0u;
                VSConstants.S_OK

            member x.GetDisplayName(pbstrName) =
                pbstrName <- "Keyword";
                VSConstants.S_OK }

[<Guid(Guids.guidFsiLanguageService)>]
type internal FsiLanguageService() = 
    inherit LanguageService()

    let mutable preferences        = null : LanguagePreferences     
    let mutable scanner            = null : IScanner
    let mutable sessions           = None : Session.FsiSessions option
    let mutable readOnlySpanGetter = (fun () -> new TextSpan())

    let readOnlySpan() = readOnlySpanGetter() // do not eta-contract, readOnlySpanGetter is mutable.
    member this.ReadOnlySpanGetter with set x = readOnlySpanGetter <- x
    member this.Sessions with set x = sessions <- Some x
    
    override this.GetLanguagePreferences() =
        if isNull preferences then
            preferences <- new LanguagePreferences(this.Site,
                                                   typeof<FsiLanguageService>.GUID,
                                                   this.Name)
            preferences.Init()
        preferences.EnableCodeSense <- false
        preferences
        
    override this.GetScanner(buffer:IVsTextLines) =
        if isNull scanner then
            scanner <- (new FsiScanner(buffer) :> IScanner)
        scanner
        
    override this.ParseSource(_:ParseRequest) =
        (new FsiAuthoringScope(sessions,readOnlySpan) :> AuthoringScope)
                
    override this.Name = "FSharpInteractive" // LINK: see ProvidePackage attribute on the package.
    
    override this.GetFormatFilterList() = ""

    // Reading MPF sources suggest this is called by codeWinMan.OnNewView(textView) to install a ViewFilter on the TextView.    
    override this.CreateViewFilter(mgr:CodeWindowManager,newView:IVsTextView) = new FsiViewFilter(mgr,newView) :> ViewFilter

    override this.GetItemCount count =
        count <- 1
        VSConstants.S_OK
        
    override this.GetColorableItem(_, colorableItem) =
        colorableItem <- Helpers.FsiKeyword
        VSConstants.S_OK

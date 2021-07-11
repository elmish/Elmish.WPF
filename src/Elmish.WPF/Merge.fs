module internal Elmish.WPF.Merge

open System.Collections.Generic
open System.Collections.ObjectModel


let keyed
      getSourceId
      getTargetId
      create
      update
      (target: ObservableCollection<_>)
      (source: _ array) =
  (*
   * Based on Elm's HTML.keyed
   * https://guide.elm-lang.org/optimization/keyed.html
   * https://github.com/elm/virtual-dom/blob/5a5bcf48720bc7d53461b3cd42a9f19f119c5503/src/Elm/Kernel/VirtualDom.js#L980-L1226
   *)
  let removals = Dictionary<_, _> ()
  let additions = Dictionary<_, _> ()

  let recordRemoval curTargetIdx curTarget curTargetId =
    removals.Add(curTargetId, (curTargetIdx, curTarget))
  let recordAddition curSourceIdx curSource curSourceId =
    additions.Add(curSourceId, (curSourceIdx, curSource))

  let mutable curSourceIdx = 0
  let mutable curTargetIdx = 0

  let mutable shouldContinue = true

  let sourceCount = source.Length
  let targetCount = target.Count

  while (shouldContinue && curSourceIdx < sourceCount && curTargetIdx < targetCount) do
    let curSource = source.[curSourceIdx]
    let curTarget = target.[curTargetIdx]

    let curSourceId = getSourceId curSource
    let curTargetId = getTargetId curTarget

    if curSourceId = curTargetId then
      update curTarget curSource curTargetIdx

      curSourceIdx <- curSourceIdx + 1
      curTargetIdx <- curTargetIdx + 1
    else
      let mNextSource =
        source
        |> Array.tryItem (curSourceIdx + 1)
        |> Option.map (fun s ->
          let id = getSourceId s
          s, id, id = curTargetId) // true => need to add

      let mNextTarget =
        if curTargetIdx + 1 < targetCount then target.[curTargetIdx + 1] |> Some else None
        |> Option.map (fun t ->
          let id = getTargetId t
          t, id, id = curSourceId) // true => need to remove

      match mNextSource, mNextTarget with
      | Some (nextSource, _, true), Some (nextTarget, _, true) -> // swap adjacent
          target.[curTargetIdx] <- nextTarget
          target.[curTargetIdx + 1] <- curTarget

          update curTarget nextSource (curTargetIdx + 1)
          update nextTarget curSource curTargetIdx
          
          curSourceIdx <- curSourceIdx + 2
          curTargetIdx <- curTargetIdx + 2
      |               None, Some (nextTarget, _, true)
      | Some (_, _, false), Some (nextTarget, _, true) -> // remove
          recordRemoval curTargetIdx curTarget curTargetId
          
          update nextTarget curSource curTargetIdx

          curSourceIdx <- curSourceIdx + 1
          curTargetIdx <- curTargetIdx + 2
      | Some (nextSource, _, true), None
      | Some (nextSource, _, true), Some (_, _, false) -> // add
          recordAddition curSourceIdx curSource curSourceId
          
          update curTarget nextSource (curTargetIdx + 1)
          
          curSourceIdx <- curSourceIdx + 2
          curTargetIdx <- curTargetIdx + 1
      | Some (_, _, false),               None
      |               None, Some (_, _, false)
      |               None,               None -> // source and target have different lengths and we have reached the end of one
          shouldContinue <- false 
      | Some (nextSource, nextSourceId, false), Some (nextTarget, nextTargetId, false) ->
          if nextSourceId = nextTargetId then // replace
            recordRemoval curTargetIdx curTarget curTargetId
            recordAddition curSourceIdx curSource curSourceId
            
            update nextTarget nextSource (curTargetIdx + 1)

            curSourceIdx <- curSourceIdx + 2
            curTargetIdx <- curTargetIdx + 2
          else // collections very different
            shouldContinue <- false

  // replace many
  while (curSourceIdx < sourceCount && curTargetIdx < targetCount) do
    let curSource = source.[curSourceIdx]
    let curTarget = target.[curTargetIdx]

    let curSourceId = getSourceId curSource
    let curTargetId = getTargetId curTarget

    recordRemoval curTargetIdx curTarget curTargetId
    recordAddition curSourceIdx curSource curSourceId
    
    curSourceIdx <- curSourceIdx + 1
    curTargetIdx <- curTargetIdx + 1

  // remove many
  for i in targetCount - 1..-1..curTargetIdx do
    let t = target.[i]
    let id = getTargetId t
    recordRemoval i t id

  // add many
  for i in curSourceIdx..sourceCount - 1 do
    let s = source.[i]
    let id = getSourceId s
    recordAddition i s id

  let moves =
    additions
    |> Seq.toList // make copy of additions so that calling Remove doesn't happen on the same data structure while enumerating
    |> List.collect (fun (Kvp (id, (sIdx, s))) ->
      removals
      |> Dictionary.tryFind id
      |> Option.map (fun (tIdx, t) ->
          removals.Remove id |> ignore
          additions.Remove id |> ignore
          (tIdx, sIdx, t, s) |> List.singleton)
      |> Option.defaultValue [])

  let actuallyRemove () =
    Seq.empty
    |> Seq.append (removals |> Seq.map (Kvp.value >> fst))
    |> Seq.append (moves |> Seq.map (fun (tIdx, _, _, _) -> tIdx))
    |> Seq.sortDescending // remove by index from largest to smallest
    |> Seq.iter target.RemoveAt

  let actuallyAdd () =
    Seq.empty
    |> Seq.append (additions |> Seq.map (fun (Kvp (id, (idx, s))) -> idx, create s id))
    |> Seq.append (moves |> Seq.map (fun (_, sIdx, t, _) -> sIdx, t))
    |> Seq.sortBy fst // insert by index from smallest to largest
    |> Seq.iter target.Insert

  match moves, removals.Count, additions.Count with
  | [ (tIdx, sIdx, _, _) ], 0, 0 -> // single move
      target.Move(tIdx, sIdx)
  | [ (t1Idx, s1Idx, _, _); (t2Idx, s2Idx, _, _) ], 0, 0 when t1Idx = s2Idx && t2Idx = s1Idx-> // single swap
      let temp = target.[t1Idx]
      target.[t1Idx] <- target.[t2Idx]
      target.[t2Idx] <- temp
  | _, rc, _ when rc = targetCount && rc > 0 -> // remove everything (implies moves = [])
      target.Clear ()
      actuallyAdd ()
  | _ ->
      actuallyRemove ()
      actuallyAdd ()

  // update moved elements
  moves |> Seq.iter (fun (_, sIdx, t, s) -> update t s sIdx)
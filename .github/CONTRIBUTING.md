Contributor guidelines
======================

First of all – thanks for taking the time to contribute!

We welcome the contributions from non-members. That said, we’d like to do things right rather than fast. To make everyone's experience as enjoyable as possible, please keep the following things in mind:

- Unless it's a trivial fix, consider opening an issue first to discuss it with the team.
- For all pull requests, please follow the workflow described below.

Opening an issue
----------------

- Before opening an issue, please check if there's a known workaround, existing issue, or already a work in progress to address it.
- Provide as much relevant info as possible. Follow the template if it makes sense.

Creating a pull request
-----------------------

(Based on https://github.com/App-vNext/Polly/wiki/Git-Workflow)

To contribute to Elmish.WPF while ensuring a smooth experience for all involved, please ensure you follow all of these steps:

1. Fork Elmish.WPF on GitHub
2. Clone your fork locally
3. Add the upstream repo: `git remote add upstream git@github.com:elmish/Elmish.WPF.git`
4. Create a local branch: `git checkout -b myBranch`
5. Work on your feature
6. Rebase if required (see below)
7. Push the branch up to GitHub: `git push origin myBranch`
8. Send a Pull Request on GitHub

You should **never** work on a clone of master, and you should **never** send a pull request from master - always from a branch. The reasons for this are detailed below.

### Rebasing when handling updates from `upstream/master`

While you're working on your branch it's quite possible that your upstream master may be updated. If this happens you should:

1. [Stash](https://git-scm.com/book/en/v2/Git-Tools-Stashing-and-Cleaning) any un-committed changes you need to
2. `git checkout master`
3. `git pull upstream master`
4. `git rebase master myBranch`
5.  `git push origin master` (optional; this this makes sure your remote master is up to date)

This ensures that your history is “clean”, with one branch off from master containing your changes in a straight line. Failing to do this ends up with several messy merges in your history, which we’d rather avoid in order to keep the project history understandable. This is the reason why you should always work in a branch and you should never be working in, or sending pull requests from, `master`.

If you have pushed your branch to GitHub and you need to rebase like this (including after you have created a pull request), you need to use `git push -f` to force rewrite the remote branch.

Also considering cleaning your commit history by squashing commits in an interactive rebase.

More on rebasing and squashing can be found in [this guide](https://robots.thoughtbot.com/git-interactive-rebase-squash-amend-rewriting-history).

Miscellaneous information
-------------------------

### Branches in the main repo

`master` is the primary development branch in the main repo. Any new features and fixes are merged into master when they are done. `master` should always be fully functional, but releases do not happen automatically from this branch.

The  `stable-` prefixed branches are what’s actually deployed. When releasing a new version, changes from `master` are merged into the latest `stable-` branch. AppVeyor will automatically deploy all new commits to these branches after a successful build and test run.

### Deployment checklist

For maintainers.

* Make changes as necessary and merge into master. **The changes must include updated release notes with a new version number**, as this is automatically used as the Nuget version when deploying.
* Create a PR from `master` to the latest `stable-` branch
* Merge if the tests pass.

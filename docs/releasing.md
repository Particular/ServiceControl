## ServiceControl releasing procedure

### 1. Merge change
ServiceControl uses [githubflow for versioning](http://gitversion.readthedocs.io/en/latest/git-branching-strategies/githubflow/), so there is no `develop` branch.
So any fix/feature is worked on a branch/PR (there is no convention for naming these branches) and those get merged directly into `master`.

By default a merge directly into master is considered a `patch` increment, if u want to up the `minor` or `major`, you merge the PR to a `release branch` (read section "Branch name" in http://gitversion.readthedocs.io/en/latest/more-info/version-increments/) and then merge that to `master`, or alternative u can set the `next-version` (read section "GitVersion.yml" in http://gitversion.readthedocs.io/en/latest/more-info/version-increments/) in the [yaml file](https://github.com/Particular/ServiceControl/blob/master/gitversionconfig.yaml).

### 2. Build using TeamCity
Once you have your change merged into `master` (AVOID BUNDLING CHANGES, RELEASE OFTEN INSTEAD) and the [build is green](https://builds.particular.net/project.html?projectId=ServicePulse) it is time to "Promote" the master build to "Deploy".
![Promote](Promote.PNG)
![Promote_dialog](Promote_dialog.PNG)

### 3. Deploy using OctopusDeploy
You are now ready to go to [OctopusDeploy](http://deploy.particular.net/app#/projects/servicepulse).
You should have a new "Staging" build waiting to be released.  
![Octopus_staging](Octopus_staging.PNG)

First step is to proof read the release notes that were auto generated (it is best to use an outsider for this job).  
![Octopus assign to me](Octopus%20assign%20to%20me.PNG)

This release notes are generated based on the issues/PRs titles associated to a milestone that matches the version you are about to release. 
By default the release notes are very "simple", if you want you can edit them and add extra content to it to make them more appealing, see example [here](https://github.com/Particular/ServiceControl/releases/tag/1.10.0) and [here](https://github.com/Particular/ServicePulse/releases/tag/1.2.0).

**NOTE**: Ensure all issues/PRs are closed, otherwise they are not included in the release notes.


**NOTE 2**: The link at the end needs to be updated to:
```
You can download this release from our [website](http://particular.net/downloads).
```

Once you happy approve them:  
![looks good](looks%20good.PNG)

And then all is left to do is "Promote to Production".  
![promote to prod](promote%20to%20prod.PNG)

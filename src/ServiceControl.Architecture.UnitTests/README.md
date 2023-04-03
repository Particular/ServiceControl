# Architecture Fitness Functions

The book [Software Architecture: The Hard Parts](https://www.oreilly.com/library/view/software-architecture-the/9781492086888/) outlines a process for decomposing a monolith by going through a sequence of patterns. Each pattern is supported by [Architecture Fitness Functions](https://www.thoughtworks.com/en-au/radar/techniques/architectural-fitness-function). This project aims to implement these fitness functions.

**If you are working in this repository and cause a failure in one of these approval tests, it is OK to approve it and release.**

**Consider the outliers identified by these approval tests as good targets for componentization.**

## Identify and Size Components

Components are identified by namespace. They are roughly sized by _statement count_ which is estimated as number of lines containing a `;`. Actual size does not really matter as long as compoent sizes can be compared.

### Fitness Function: Maintain component inventory

It is easier to manage components if we know what they all are. This test will ensure that we know when components are added or removed from the codebase.

### Fitness Function: No component shall exceed <some percent> of overall codebase

It is easier to manage smaller components. Components which make up a large proprotion of the codebase are an architectural smell. Consider breaking these components up into smaller components.

### Fitness Function: No component shall exceed <som number of standard deviations from the mean component size

Components which are much bigger than the others are an architectural smell. Consider breaking outliers up into smaller components.

## Gather Common Domain Components

_To be implemented_

## Flatten Components

_To be implemented_

## Determine Component Dependencies

_To be implemented_

## Create Component Domains

_To be implemented_

## Create Domain Services

_To be implemented_
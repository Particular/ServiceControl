# Testing tool

## Goal

The goal is to create a testing tool to simulate load and real world scenarios to be deployed alongside a test instance of service control to be able to test the error ingestion performance

## Background

Some tools have been made to assist, use these for reference

* https://github.com/dvdstelt/ServiceControlFeeder
* https://github.com/ramonsmits/FakeMessageGen

## Requirements

*Functional*

* should be able to simulate high error loads, this will have to bypass actually creating the initial messages
* should be able to generate errors via a real message handler isntead of simulated load 
* the error generation should be based on some real scenarios, so that we still have nice groups of errors, example a third party outage
* Everything should explose otel
* A background job that every so often replays error groups (and these should then pass)
* A background job that every so often does a search (hopefully exercising FTS)
* (optional) any scenarios from the release tests should be bonsidered to kick off manually

*nonfunctional*

* hosted in a container
* simple web ui for kicking off manual scenarios
* stateless
* can be scaled horizontally

## out of scope 

* Audit testing
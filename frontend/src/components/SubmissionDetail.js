import React from "react";

export default function SubmissionDetail({ submission }) {
  return (
    <div className="submission-detail card mt-3">
      <div className="card-body">
        <h5 className="card-title">{submission.industry}</h5>
        <p className="card-text">
          <strong>Business Process Description:</strong>{" "}
          {submission.businessProcessDescription}
        </p>
        <p className="card-text">
          <strong>Process Frequency:</strong> {submission.processFrequency}
        </p>
        <p className="card-text">
          <strong>Process Duration:</strong> {submission.processDuration}
        </p>
      </div>
    </div>
  );
}

import React, { useState, useEffect } from "react";
import axios from "axios";

export default function SubmissionDetail({ submission }) {
  const [currentSubmission, setCurrentSubmission] = useState(submission);
  const [showReason, setShowReason] = useState(false);
  const [reason, setReason] = useState("");

  // Update currentSubmission when a new submission is selected
  useEffect(() => {
    setCurrentSubmission(submission);
  }, [submission]);

  // Polling to fetch the message if it's empty
  useEffect(() => {
    let intervalId;

    if (!currentSubmission.message) {
      intervalId = setInterval(async () => {
        try {
          const response = await axios.get(
            `/api/lead-form-submissions/${currentSubmission.id}`
          );
          setCurrentSubmission(response.data);

          // Stop polling if message is available
          if (response.data.message) {
            clearInterval(intervalId);
          }
        } catch (error) {
          console.error("Error fetching submission:", error);
        }
      }, 5000); // Poll every 5 seconds
    }

    // Cleanup interval on component unmount or when submission changes
    return () => clearInterval(intervalId);
  }, [currentSubmission]);

  const handleAction = async (approved) => {
    const payload = {
      instanceId: currentSubmission.instanceId,
      approved: approved,
      ...(approved ? {} : { reason }), // Add reason only if rejected
    };

    try {
      await axios.post(
        "https://ignitedemosendemailfunctionapp.azurewebsites.net/api/TriggerApproval",
        payload
      );
      alert(`Submission ${approved ? "approved" : "rejected"} successfully!`);
      setShowReason(false); // Reset the reason box visibility
      setReason(""); // Clear the reason text
    } catch (error) {
      console.error("Error sending request:", error);
      alert(
        "Failed to send request. Please try again. Instance ID: " +
          currentSubmission.instanceId +
          " Approved: " +
          approved
      );
    }
  };

  const handleRejectClick = () => {
    setShowReason(true); // Show the reason text box
  };

  const handleReasonSubmit = () => {
    if (!reason.trim()) {
      alert("Please provide a reason for rejection.");
      return;
    }
    handleAction(false); // Submit rejection with reason
  };

  return (
    <div className="submission-detail card mt-3">
      <div className="card-body">
        <h5 className="card-title">{currentSubmission.industry}</h5>
        <p className="card-text">
          <strong>Business Process Description:</strong>{" "}
          {currentSubmission.businessProcessDescription}
        </p>
        <p className="card-text">
          <strong>Process Frequency:</strong> {currentSubmission.processFrequency}
        </p>
        <p className="card-text">
          <strong>Process Duration:</strong> {currentSubmission.processDuration}
        </p>
        <p className="card-text">
          <strong>Proposed email:</strong>{" "}
          {currentSubmission.message ? (
            currentSubmission.message
          ) : (
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          )}
        </p>
        <div className="mt-4">
          <button
            className="btn btn-success me-2"
            onClick={() => handleAction(true)}
          >
            Approve
          </button>
          <button className="btn btn-danger" onClick={handleRejectClick}>
            Reject
          </button>
        </div>
        {showReason && (
          <div className="mt-3">
            <textarea
              className="form-control"
              placeholder="Provide a reason for rejection"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows="3"
            />
            <button
              className="btn btn-primary mt-2"
              onClick={handleReasonSubmit}
            >
              Submit Rejection
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

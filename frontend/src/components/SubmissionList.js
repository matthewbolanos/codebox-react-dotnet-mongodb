import React from "react";

export default class SubmissionList extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      activeIndex: null,
    };
  }

  handleActive(index, submission) {
    this.setState({ activeIndex: index });
    this.props.onSelect(submission);
  }

  renderSubmissions(submissions) {
    return (
      <ul className="list-group">
        {submissions.map((submission, i) => (
          <li
            className={
              "list-group-item list-group-item-action " +
              (i === this.state.activeIndex ? "active" : "")
            }
            key={i}
            onClick={() => this.handleActive(i, submission)}
          >
            <div className="d-flex w-100 justify-content-between align-items-center">
              <h5 className="mb-1">{submission.industry}</h5>
              {submission.status && (
                <small
                  className={`status-badge ${
                    submission.status === "approved"
                      ? "text-success"
                      : submission.status === "pending"
                      ? "text-warning"
                      : "text-secondary"
                  }`}
                >
                  {submission.status.charAt(0).toUpperCase() +
                    submission.status.slice(1)}
                </small>
              )}
            </div>
            <p className="mb-1">{submission.businessProcessDescription}</p>
          </li>
        ))}
      </ul>
    );
  }

  render() {
    const { submissions } = this.props;
    return (submissions || []).length > 0 ? (
      this.renderSubmissions(submissions)
    ) : (
      <div className="alert alert-primary" role="alert">
        No submissions to display
      </div>
    );
  }
}

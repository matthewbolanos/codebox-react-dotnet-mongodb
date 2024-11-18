import React from "react";

export default class SubmissionList extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      activeIndex: 0,
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
              "list-group-item cursor-pointer " +
              (i === this.state.activeIndex ? "active" : "")
            }
            key={i}
            onClick={() => this.handleActive(i, submission)}
          >
            <strong>{submission.industry}</strong> -{" "}
            {submission.businessProcessDescription}
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

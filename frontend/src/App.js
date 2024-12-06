import React from "react";
import axios from "axios";
import "./App.scss";
import SubmissionList from "./components/SubmissionList";
import SubmissionDetail from "./components/SubmissionDetail";

export default class App extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      submissions: [],
      selectedSubmission: null,
    };
  }

  componentDidMount() {
    axios
      .get("/api/lead-form-submissions")
      .then((response) => {
        this.setState({
          submissions: response.data,
        });
      })
      .catch((e) => console.log("Error : ", e));
  }

  handleSelectSubmission = (submission) => {
    this.setState({ selectedSubmission: submission });
  };

  render() {
    const { submissions, selectedSubmission } = this.state;

    return (
      <div className="App container">
        <div className="row">
          <div className="col-md-4">
            <h2>Submissions</h2>
            <SubmissionList
              submissions={submissions}
              onSelect={this.handleSelectSubmission}
            />
          </div>
          <div className="col-md-8">
            {selectedSubmission ? (
              <SubmissionDetail submission={selectedSubmission} />
            ) : (
              <div className="alert alert-info mt-3">
                Please select a submission to view details.
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }
}

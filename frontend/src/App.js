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
        <div className="container-fluid">
          <div className="row">
            <div className="col-xs-12 col-sm-8 col-md-8 offset-md-2">
              <h1>Submissions</h1>
              <div className="todo-app">
                <SubmissionList
                  submissions={submissions}
                  onSelect={this.handleSelectSubmission}
                />
                {selectedSubmission && (
                  <SubmissionDetail submission={selectedSubmission} />
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }
}
